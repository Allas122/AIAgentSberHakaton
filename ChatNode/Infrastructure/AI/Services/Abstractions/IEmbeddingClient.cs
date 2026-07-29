namespace ChatNode.Infrastructure.AI.Services.Abstractions;

public interface IEmbeddingClient
{
    public Task<IEnumerable<IReadOnlyList<double>>> GetEmbeddingsAsync(IReadOnlyList<string> strings);
    public Task<IReadOnlyList<double>> GetEmbeddingAsync(string strings);
}