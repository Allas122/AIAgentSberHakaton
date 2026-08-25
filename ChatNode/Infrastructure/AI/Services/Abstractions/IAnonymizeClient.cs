namespace ChatNode.Infrastructure.AI.Services.Abstractions;

public interface IAnonymizeClient
{
    Task<string> AnonymizeAsync(string text, string sessionId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> AnonymizeBatchAsync(
        IReadOnlyList<string> texts,
        string sessionId,
        CancellationToken ct = default);

    Task<string> DeanonymizeAsync(string text, string sessionId, CancellationToken ct = default);
}
