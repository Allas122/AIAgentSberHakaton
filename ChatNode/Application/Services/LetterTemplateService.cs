using System.Text;
using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Database.Configurations;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class LetterTemplateService(
    ILetterTemplateRepository repository,
    IDocxTextExtractor docxTextExtractor,
    IDocxTemplateFiller templateFiller,
    IFileStorage fileStorage,
    IOrganizationProfileService organizationProfileService,
    ILogger<LetterTemplateService> logger) : ILetterTemplateService
{
    public const int MinContentLength = 20;
    public const int MaxTemplates = 50;

    private static readonly string[] TextExtensions = [".txt", ".md"];
    private const string DocxExtension = ".docx";

    public async Task<IReadOnlyList<LetterTemplateDto>> ListAsync(Guid ownerId) =>
        [.. LetterPresets.All, .. (await repository.ListAsync(ownerId)).Select(Map)];

    public async Task<LetterTemplateDto> CreateAsync(
        Guid ownerId,
        CreateLetterTemplateDto dto,
        CancellationToken ct = default)
    {
        var name = Trim(dto.Name);

        if (name.Length == 0)
        {
            throw new InvalidRequestException("У шаблона должно быть название.");
        }

        if ((await repository.ListAsync(ownerId)).Count >= MaxTemplates)
        {
            throw new InvalidRequestException($"Шаблонов не может быть больше {MaxTemplates}.");
        }

        if (await repository.NameTakenAsync(ownerId, name))
        {
            throw new InvalidRequestException($"Шаблон с названием «{name}» уже есть.");
        }

        var file = dto.FileStream is not null ? await ReadBytesAsync(dto.FileStream, ct) : null;

        var content = file is not null
            ? ReadFile(file, dto.FileName)
            : Trim(dto.Content ?? string.Empty);

        ValidateContent(content);

        var template = new LetterTemplate
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            Content = content,
            SourceFileName = dto.FileStream is not null ? dto.FileName : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (file is not null && IsDocx(dto.FileName))
        {
            await AttachFormAsync(template, file, dto.FileName, ct);
        }

        await repository.CreateAsync(template);

        return Map(template);
    }

    public async Task<LetterTemplateDto> UpdateAsync(Guid ownerId, Guid templateId, UpdateLetterTemplateDto dto)
    {
        DenyPreset(templateId);

        var template = await LoadAsync(ownerId, templateId);

        if (dto.Name is not null)
        {
            var name = Trim(dto.Name);
            if (name.Length == 0) throw new InvalidRequestException("Название не может быть пустым.");

            if (await repository.NameTakenAsync(ownerId, name, templateId))
            {
                throw new InvalidRequestException($"Шаблон с названием «{name}» уже есть.");
            }

            template.Name = name;
        }

        if (dto.Content is not null)
        {
            var content = Trim(dto.Content);
            ValidateContent(content);
            template.Content = content;
            template.SourceFileName = null;
        }

        if (!await repository.UpdateAsync(template))
        {
            throw new NotFoundException("Шаблон не найден.");
        }

        return Map(template);
    }

    public async Task DeleteAsync(Guid ownerId, Guid templateId)
    {
        DenyPreset(templateId);

        var template = await LoadAsync(ownerId, templateId);

        if (!await repository.DeleteAsync(templateId))
        {
            throw new NotFoundException("Шаблон не найден.");
        }

        if (template.FormStorageKey is not { } key) return;

        try
        {
            await fileStorage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Бланк шаблона {TemplateId} остался в хранилище: {Key}", templateId, key);
        }
    }

    public async Task<DocumentFileDto> PreviewAsync(Guid ownerId, Guid templateId, CancellationToken ct = default)
    {
        var template = await LoadAsync(ownerId, templateId);

        if (template.FormStorageKey is not { } key)
        {
            throw new InvalidRequestException(
                "У этого шаблона нет бланка. Загрузите .docx с плейсхолдерами, например <ТЕЛО>.");
        }

        await using var stream = await fileStorage.DownloadAsync(key, ct);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);

        var profile = await organizationProfileService.GetAsync(ownerId, ct);

        var addressee = new LetterAddresseeDto(
            "И.И. Иванову",
            "Директору департамента",
            "Иван Иванович",
            null);

        var values = LetterFormValues.Build(
            profile,
            addressee,
            "Уважаемый Иван Иванович!",
            new LetterRequisitesDto("01-16/0000", DateTimeOffset.Now.ToString("dd.MM.yyyy"), null, null),
            PreviewBody);

        var filled = templateFiller.Fill(buffer.ToArray(), values);

        logger.LogInformation(
            "Проверка бланка {TemplateId}: заполнено {Filled}, пусто {Empty}, неизвестных меток {Unknown}",
            templateId,
            filled.Filled.Count,
            filled.LeftEmpty.Count,
            filled.Unknown.Count);

        return new DocumentFileDto(filled.Content, $"proverka-blanka-{DateTimeOffset.Now:yyyy-MM-dd}.docx");
    }

    private const string PreviewBody =
        "Это проверочный текст: так на бланке будет выглядеть тело письма. " +
        "Абзац набран обычным машинописным текстом без разметки.\n" +
        "Второй абзац показывает, что многострочный ответ разбивается на абзацы, " +
        "а не склеивается в один блок.\n" +
        "Третий абзац — чтобы было видно интервалы и выключку по ширине.";

    private static void DenyPreset(Guid templateId)
    {
        if (LetterPresets.IsPreset(templateId))
        {
            throw new InvalidRequestException(
                "Это встроенный вид письма — его нельзя изменить или удалить. " +
                "Скопируйте его текст в свой шаблон и правьте копию.");
        }
    }

    private async Task<LetterTemplate> LoadAsync(Guid ownerId, Guid templateId)
    {
        var template = await repository.GetAsync(templateId)
                       ?? throw new NotFoundException("Шаблон не найден.");

        if (template.OwnerId != ownerId)
        {
            throw new PermissionDenied("Этот шаблон создал другой пользователь.");
        }

        return template;
    }

    private static bool IsDocx(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).Equals(DocxExtension, StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]> ReadBytesAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);

        return buffer.ToArray();
    }

    private async Task AttachFormAsync(LetterTemplate template, byte[] file, string? fileName, CancellationToken ct)
    {
        var placeholders = templateFiller.Scan(file);

        if (placeholders.Count == 0)
        {
            logger.LogInformation(
                "Шаблон {Name}: плейсхолдеров в .docx нет, бланк не сохраняю — остаётся текстовым шаблоном",
                template.Name);

            return;
        }

        var key = StorageKeys.LetterForm(template.OwnerId, template.Id);

        await fileStorage.UploadAsync(key, file, ct);

        template.FormStorageKey = key;
        template.FormFileName = fileName;
        template.FormSizeBytes = file.Length;
        template.FormPlaceholders = string.Join(",", placeholders);

        logger.LogInformation(
            "Шаблон {Name}: бланк сохранён, плейсхолдеров {Count} ({Placeholders})",
            template.Name,
            placeholders.Count,
            template.FormPlaceholders);
    }

    private string ReadFile(byte[] file, string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        using var stream = new MemoryStream(file, writable: false);

        if (extension == DocxExtension)
        {
            var lines = docxTextExtractor.ExtractLines(stream);
            return Trim(string.Join("\n", lines));
        }

        if (TextExtensions.Contains(extension))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Trim(reader.ReadToEnd());
        }

        throw new InvalidDocumentException("Шаблон принимается в формате .txt или .docx.");
    }

    private static void ValidateContent(string content)
    {
        if (content.Length < MinContentLength)
        {
            throw new InvalidRequestException(
                $"Текст шаблона слишком короткий — нужно хотя бы {MinContentLength} символов. " +
                "Если загружали файл, возможно, в нём только картинки.");
        }

        if (content.Length > LetterTemplateConfiguration.MaxContentLength)
        {
            throw new InvalidRequestException(
                $"Текст шаблона длиннее {LetterTemplateConfiguration.MaxContentLength} символов.");
        }
    }

    private static string Trim(string value) => value.Replace("\r\n", "\n").Trim();

    private static LetterTemplateDto Map(LetterTemplate template) =>
        new(template.Id,
            template.Name,
            template.Content,
            template.SourceFileName,
            template.CreatedAt,
            template.UpdatedAt,
            IsPreset: false,
            HasForm: template.FormStorageKey is not null,
            Placeholders: Split(template.FormPlaceholders));

    private static IReadOnlyList<string> Split(string? placeholders) =>
        string.IsNullOrWhiteSpace(placeholders)
            ? []
            : placeholders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
