using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using ChatNode.Infrastructure.Tools;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class LetterService(
    AgentFactory agentFactory,
    IDocxTextExtractor docxTextExtractor,
    IPdfTextExtractor pdfTextExtractor,
    IDocxTemplateFiller templateFiller,
    IAnonymizeClient anonymizeClient,
    ILetterTemplateRepository templateRepository,
    IOrganizationProfileService organizationProfileService,
    IFileStorage fileStorage,
    IDocumentRegistry documentRegistry,
    ILogger<LetterService> logger) : ILetterService
{
    private const string DocxExtension = ".docx";
    private const string PdfExtension = ".pdf";

    public async Task<LetterReplyDto> ComposeReplyAsync(ComposeLetterDto request, CancellationToken ct)
    {
        var (userId, fileStream, fileName, letterText, intent, templateId, addressee, requisites) = request;

        var sessionId = $"letter:{userId}:{Guid.NewGuid()}";

        var lines = fileStream is not null
            ? ExtractLines(fileStream, fileName)
            : SplitLines(letterText);

        var anonymizedLetter = lines.Count == 0
            ? string.Empty
            : string.Join("\n", await anonymizeClient.AnonymizeBatchAsync(lines, sessionId, ct));

        if (string.IsNullOrWhiteSpace(anonymizedLetter))
        {
            throw new InvalidDocumentException(
                "Не удалось прочитать текст письма — файл пустой, состоит из картинок или это скан без текстового слоя.");
        }

        var anonymizedIntent = string.IsNullOrWhiteSpace(intent)
            ? null
            : await anonymizeClient.AnonymizeAsync(intent, sessionId, ct);

        var stored = await LoadTemplateAsync(userId, templateId);
        var template = stored?.Content;

        var profile = await organizationProfileService.GetAsync(userId, ct);
        var salutation = BuildSalutation(addressee, out var genderGuess);

        var context = new LetterContextDto(
            MapOrganization(profile),
            MapAddressee(addressee, salutation));

        var mode = stored?.FormStorageKey is null ? LetterOutputMode.FullText : LetterOutputMode.BodyOnly;

        var agent = agentFactory.CreateLetterAgent(new LetterSession(userId, sessionId));
        var result = await agent.ComposeReplyAsync(anonymizedLetter, anonymizedIntent, template, context, mode, ct);

        var reply = await anonymizeClient.DeanonymizeAsync(result.Reply, sessionId, ct);

        var assignments = result.Assignments
            .Select(a => new LetterAssignmentDto(a.Id, a.Title, a.Assignee, a.DueDate, a.Status))
            .ToList();

        var warnings = BuildWarnings(profile, addressee, salutation, genderGuess);

        if (mode == LetterOutputMode.FullText)
        {
            return new LetterReplyDto(reply, assignments, warnings, null, null);
        }

        reply = StripSalutation(reply, warnings);

        var document = await RenderAsync(userId, stored!, profile, addressee, salutation, requisites, reply, warnings, ct);

        return new LetterReplyDto(reply, assignments, warnings, document?.DocumentId, document?.FileName);
    }

    private async Task<(Guid? DocumentId, string FileName)?> RenderAsync(
        Guid userId,
        LetterTemplate template,
        OrganizationProfileDto profile,
        LetterAddresseeDto? addressee,
        string? salutation,
        LetterRequisitesDto requisites,
        string body,
        List<string> warnings,
        CancellationToken ct)
    {
        byte[] form;

        try
        {
            await using var stream = await fileStorage.DownloadAsync(template.FormStorageKey!, ct);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            form = buffer.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Бланк шаблона {TemplateId} недоступен в хранилище", template.Id);
            warnings.Add("Бланк недоступен в хранилище — файл не сформирован, ниже только текст ответа.");

            return null;
        }

        var values = LetterFormValues.Build(profile, addressee, salutation, requisites, body);
        var filled = templateFiller.Fill(form, values);

        if (filled.LeftEmpty.Count > 0)
        {
            warnings.Add("В бланке остались незаполненными: " + string.Join(", ", filled.LeftEmpty) + ".");
        }

        if (filled.Unknown.Count > 0)
        {
            warnings.Add("В бланке есть метки, которые система не заполняет: " + string.Join(", ", filled.Unknown) + ".");
        }

        if (AnonymizationTags.Contains(body))
        {
            warnings.Add("В тексте остались служебные метки обезличивания — проверьте письмо перед отправкой.");
        }

        var letterId = Guid.NewGuid();
        var key = StorageKeys.GeneratedLetter(userId, letterId);
        var fileName = FileNameOf(requisites);

        await fileStorage.UploadAsync(key, filled.Content, ct);

        var documentId = await documentRegistry.RecordAsync(
            key,
            StoredDocumentKind.GeneratedDocument,
            userId,
            null,
            fileName,
            filled.Content.Length,
            ct);

        if (documentId is null)
        {
            warnings.Add("Письмо сформировано, но не попало в реестр файлов — скачать его не получится.");
        }

        logger.LogInformation(
            "Письмо на бланке готово: шаблон={TemplateId}, заполнено={Filled}, пусто={Empty}, размер={Size} байт",
            template.Id,
            filled.Filled.Count,
            filled.LeftEmpty.Count,
            filled.Content.Length);

        return (documentId, fileName);
    }

    private static string StripSalutation(string reply, List<string> warnings)
    {
        var lines = reply.Replace("\r\n", "\n").Split('\n');

        if (lines.Length == 0 || !lines[0].TrimStart().StartsWith("Уважаем", StringComparison.OrdinalIgnoreCase))
        {
            return reply;
        }

        warnings.Add("Модель добавила обращение в текст — убрал его, на бланке обращение уже есть.");

        return string.Join("\n", lines.Skip(1)).TrimStart('\n');
    }

    private static string FileNameOf(LetterRequisitesDto requisites)
    {
        var parts = new[] { requisites.OutgoingNumber, requisites.OutgoingDate }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim());

        var name = string.Join("-", parts);

        name = string.IsNullOrWhiteSpace(name)
            ? $"otvet-{DateTimeOffset.Now:yyyy-MM-dd}"
            : "ish-" + name;

        var safe = new string(name.Select(symbol => Path.GetInvalidFileNameChars().Contains(symbol) ? '-' : symbol).ToArray());

        return safe + DocxExtension;
    }

    private IReadOnlyList<string> ExtractLines(Stream fileStream, string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant() switch
        {
            PdfExtension => pdfTextExtractor.ExtractLines(fileStream),
            DocxExtension or "" => docxTextExtractor.ExtractLines(fileStream),
            _ => throw new InvalidDocumentException("Письмо принимается в формате .docx или .pdf.")
        };

    private static IReadOnlyList<string> SplitLines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Replace("\r\n", "\n").Split('\n');

    private async Task<LetterTemplate?> LoadTemplateAsync(Guid userId, Guid? templateId)
    {
        if (templateId is null) return null;

        if (LetterPresets.Find(templateId.Value) is { } preset)
        {
            return new LetterTemplate { Id = preset.Id, OwnerId = userId, Name = preset.Name, Content = preset.Content };
        }

        var template = await templateRepository.GetAsync(templateId.Value)
                       ?? throw new NotFoundException("Шаблон не найден.");

        if (template.OwnerId != userId)
        {
            throw new PermissionDenied("Этот шаблон создал другой пользователь.");
        }

        return template;
    }

    private static string? BuildSalutation(LetterAddresseeDto? addressee, out GenderGuess guess)
    {
        guess = GenderGuess.Unknown;

        if (addressee is null) return null;

        guess = addressee.Gender is { } explicitGender && explicitGender != PersonGender.Unknown
            ? new GenderGuess(explicitGender, GenderSource.Explicit)
            : RussianNameGender.Detect($"{addressee.Name} {addressee.Salutation}");

        var prefix = RussianNameGender.Salutation(guess.Gender);
        if (prefix is null) return null;

        var name = !string.IsNullOrWhiteSpace(addressee.Salutation)
            ? addressee.Salutation.Trim()
            : RussianNameGender.GivenAndPatronymic(addressee.Name);

        if (string.IsNullOrWhiteSpace(name)) return null;

        var salutation = $"{prefix} {name.TrimEnd('!', ' ')}";

        return salutation + "!";
    }

    private static List<string> BuildWarnings(
        OrganizationProfileDto profile,
        LetterAddresseeDto? addressee,
        string? salutation,
        GenderGuess guess)
    {
        var warnings = new List<string>();

        if (profile.IsEmpty)
        {
            warnings.Add(
                "Карточка организации не заполнена — подпись и контактное лицо в письме остались "
                + "плейсхолдерами. Заполните её в личном кабинете.");
        }
        else
        {
            if (!profile.HasSigner) warnings.Add("В карточке организации не указан подписант — подпись осталась плейсхолдером.");
            if (!profile.HasContact) warnings.Add("В карточке организации нет контактного лица — блок контактов остался плейсхолдером.");
        }

        var hasAddressee = addressee is not null &&
                           (!string.IsNullOrWhiteSpace(addressee.Name) || !string.IsNullOrWhiteSpace(addressee.Position));

        if (!hasAddressee)
        {
            warnings.Add("Адресат не указан — блок «Кому» и обращение остались плейсхолдерами.");
        }
        else if (salutation is null)
        {
            warnings.Add(guess.Gender == PersonGender.Unknown
                ? "Пол адресата по фамилии однозначно не определяется — выберите обращение вручную."
                : "Для обращения нужны имя и отчество адресата: из инициалов они не выводятся.");
        }
        else if (guess.NeedsConfirmation)
        {
            warnings.Add($"Обращение «{salutation}» выбрано по фамилии — проверьте его перед отправкой.");
        }

        return warnings;
    }

    private static LetterOrganizationDto? MapOrganization(OrganizationProfileDto profile) =>
        profile.IsEmpty
            ? null
            : new LetterOrganizationDto(
                profile.FullName,
                profile.ShortName,
                profile.Address,
                profile.Phone,
                profile.Fax,
                profile.Email,
                profile.Website,
                profile.Okpo,
                profile.Ogrn,
                profile.Inn,
                profile.Kpp,
                profile.SignerPosition,
                profile.SignerName,
                profile.ContactName,
                profile.ContactPosition,
                profile.ContactPhone,
                profile.ContactEmail);

    private static LetterAddresseeContextDto? MapAddressee(LetterAddresseeDto? addressee, string? salutation) =>
        addressee is null
            ? null
            : new LetterAddresseeContextDto(addressee.Name, addressee.Position, salutation);
}
