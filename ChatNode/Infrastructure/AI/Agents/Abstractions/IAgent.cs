using ChatNode.Infrastructure.Dto;

namespace ChatNode.Infrastructure.AI.Agents.Abstractions;

public interface IAgent
{
    public Task<string> InvokeAsync(string prompt, IEnumerable<MessageHistoricalDto> messages,CancellationToken ct);
}