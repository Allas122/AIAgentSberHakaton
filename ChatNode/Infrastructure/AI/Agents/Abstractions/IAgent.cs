using ChatNode.Infrastructure.Dto;

namespace ChatNode.Infrastructure.AI.Agents.Abstractions;

public interface IAgent
{
    Task<string> InvokeAsync(string prompt, IEnumerable<MessageHistoricalDto> messages, CancellationToken ct) 
        => InvokeAsync(prompt, ct);
    Task<string> InvokeAsync(string prompt, CancellationToken ct);
}