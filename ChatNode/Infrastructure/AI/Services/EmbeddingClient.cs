using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.AI.Services;

public class EmbeddingClient(IGigaChatClient gigaChatClient, IOptions<GigaChatOptions> options) : IEmbeddingClient
{
    private GigaChatOptions _options = options.Value;

    public async Task<IEnumerable<IReadOnlyList<double>>> GetEmbeddingsAsync(IReadOnlyList<string> texts)
    {
        var response = await EmbedAsync(texts);
        return response.Data.Select(em => em.EmbeddingVector);
    }

    public async Task<IReadOnlyList<double>> GetEmbeddingAsync(string text)
    {
        var response = await EmbedAsync([text]);
        return response.Data.Select(em => em.EmbeddingVector).First();
    }
    
    private Task<Embeddings> EmbedAsync(IReadOnlyList<string> texts) =>
        GigaChatRetry.ExecuteAsync(
            ct => gigaChatClient.EmbeddingsAsync(texts, _options.EmbeddingModel, ct),
            $"эмбеддинги ({texts.Count} шт.)",
            _options.MaxOperationAttempts,
            _options.OperationRetryDelaySeconds,
            CancellationToken.None);
}
