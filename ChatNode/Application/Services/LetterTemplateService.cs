using System.Text;
using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Database.Configurations;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class LetterTemplateService(
    ILetterTemplateRepository repository,
    IDocxTextExtractor docxTextExtractor) : ILetterTemplateService
{
    public const int MinContentLength = 20;
    public const int MaxTemplates = 50;

    private static readonly string[] TextExtensions = [".txt", ".md"];
    private const string DocxExtension = ".docx";

    public async Task<IReadOnlyList<LetterTemplateDto>> ListAsync(Guid ownerId) =>
        (await repository.ListAsync(ownerId)).Select(Map).ToList();

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

        var content = dto.FileStream is not null
            ? ReadFile(dto.FileStream, dto.FileName)
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

        await repository.CreateAsync(template);

        return Map(template);
    }

    public async Task<LetterTemplateDto> UpdateAsync(Guid ownerId, Guid templateId, UpdateLetterTemplateDto dto)
    {
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
        await LoadAsync(ownerId, templateId);

        if (!await repository.DeleteAsync(templateId))
        {
            throw new NotFoundException("Шаблон не найден.");
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

    private string ReadFile(Stream stream, string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

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
            template.UpdatedAt);
}
