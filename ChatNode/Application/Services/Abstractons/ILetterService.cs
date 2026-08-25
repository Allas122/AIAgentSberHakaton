using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface ILetterService
{
    Task<LetterReplyDto> ComposeReplyAsync(
        Guid userId,
        Stream? fileStream,
        string? letterText,
        string? intent,
        Guid? templateId,
        CancellationToken ct);
}
