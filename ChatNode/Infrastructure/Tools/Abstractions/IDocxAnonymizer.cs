namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IDocxAnonymizer
{
    Task<byte[]> AnonymizeAsync(Stream inputStream, string sessionId, CancellationToken ct = default);
}
