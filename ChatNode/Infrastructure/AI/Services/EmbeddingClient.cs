using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using GigaChat.Net;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.AI.Services;

public class EmbeddingClient(IGigaChatClient gigaChatClient, IOptions<GigaChatOptions> options) : IEmbeddingClient
{
    private GigaChatOptions _options = options.Value;
    public async Task<IEnumerable<IReadOnlyList<double>>> GetEmbeddingsAsync(IReadOnlyList<string> texts)
    {
        var embeddings = (await gigaChatClient.EmbeddingsAsync(texts, _options.EmbeddingModel))
            .Data.Select(em => em.EmbeddingVector);
        return embeddings;
    }

    public async Task<IReadOnlyList<double>> GetEmbeddingAsync(string text)
    {
        var embeddings = (await gigaChatClient.EmbeddingsAsync([text], _options.EmbeddingModel))
            .Data.Select(em => em.EmbeddingVector);
        return embeddings.First();
    }
}