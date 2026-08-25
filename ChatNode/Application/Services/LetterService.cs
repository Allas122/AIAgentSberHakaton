using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class LetterService(
    AgentFactory agentFactory,
    IDocxAnonymizer docxAnonymizer,
    IDocxTextExtractor docxTextExtractor,
    IAnonymizeClient anonymizeClient,
    ILetterTemplateRepository templateRepository) : ILetterService
{
    public async Task<LetterReplyDto> ComposeReplyAsync(
        Guid userId,
        Stream? fileStream,
        string? letterText,
        string? intent,
        Guid? templateId,
        CancellationToken ct)
    {
        var sessionId = $"letter:{userId}:{Guid.NewGuid()}";

        var anonymizedLetter = fileStream is not null
            ? await ReadDocxAsync(fileStream, sessionId, ct)
            : await anonymizeClient.AnonymizeAsync(letterText ?? string.Empty, sessionId, ct);

        if (string.IsNullOrWhiteSpace(anonymizedLetter))
        {
            throw new InvalidDocumentException("Не удалось прочитать текст письма — файл пустой или состоит из картинок.");
        }

        var anonymizedIntent = string.IsNullOrWhiteSpace(intent)
            ? null
            : await anonymizeClient.AnonymizeAsync(intent, sessionId, ct);

        var anonymizedTemplate = await LoadTemplateAsync(userId, templateId, sessionId, ct);

        var agent = agentFactory.CreateLetterAgent(new LetterSession(userId, sessionId));
        var result = await agent.ComposeReplyAsync(anonymizedLetter, anonymizedIntent, anonymizedTemplate, ct);

        var reply = await anonymizeClient.DeanonymizeAsync(result.Reply, sessionId, ct);

        var assignments = result.Assignments
            .Select(a => new LetterAssignmentDto(a.Id, a.Title, a.Assignee, a.DueDate, a.Status))
            .ToList();

        return new LetterReplyDto(reply, assignments);
    }

    private async Task<string?> LoadTemplateAsync(
        Guid userId,
        Guid? templateId,
        string sessionId,
        CancellationToken ct)
    {
        if (templateId is null) return null;

        var template = await templateRepository.GetAsync(templateId.Value)
                       ?? throw new NotFoundException("Шаблон не найден.");

        if (template.OwnerId != userId)
        {
            throw new PermissionDenied("Этот шаблон создал другой пользователь.");
        }

        return await anonymizeClient.AnonymizeAsync(template.Content, sessionId, ct);
    }

    private async Task<string> ReadDocxAsync(Stream fileStream, string sessionId, CancellationToken ct)
    {
        var anonymizedDocx = await docxAnonymizer.AnonymizeAsync(fileStream, sessionId, ct);

        using var stream = new MemoryStream(anonymizedDocx, writable: false);
        var lines = docxTextExtractor.ExtractLines(stream);

        return string.Join("\n", lines);
    }
}
