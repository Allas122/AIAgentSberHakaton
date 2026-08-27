using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface ILetterService
{
    Task<LetterReplyDto> ComposeReplyAsync(ComposeLetterDto request, CancellationToken ct);
}
