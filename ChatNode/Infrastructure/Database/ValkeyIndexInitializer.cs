using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public class ValkeyIndexInitializer(
    IConnectionMultiplexer redis,
    ILogger<ValkeyIndexInitializer> logger,
    IOptions<GigaChatOptions> options)
    : IHostedService
{
    private const string ManualIndexName = "idx:manual_parts";
    private const string PinIndexName = "idx:pins";

    private GigaChatOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        await EnsureIndexAsync(db, ManualIndexName, CreateManualIndexAsync);
        await EnsureIndexAsync(db, PinIndexName, CreatePinIndexAsync);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task EnsureIndexAsync(IDatabase db, string indexName, Func<IDatabase, Task> create)
    {
        try
        {
            await db.ExecuteAsync("FT.INFO", indexName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            await create(db);
            logger.LogInformation("Index '{IndexName}' successfully created.", indexName);
        }
    }

    private async Task CreateManualIndexAsync(IDatabase db)
    {
        await db.ExecuteAsync("FT.CREATE", ManualIndexName,
            "ON", "HASH",
            "PREFIX", "1", "manual:",
            "SCHEMA",
            "partId", "TAG",
            "manualId", "TAG",
            "title", "TEXT",
            "navigation", "TEXT",
            "content", "TEXT",
            "embedding", "VECTOR", "HNSW", "6",
            "TYPE", "FLOAT32",
            "DIM", _options.EmbeddingDim.ToString(),
            "DISTANCE_METRIC", "COSINE"
        );
    }

    private async Task CreatePinIndexAsync(IDatabase db)
    {
        await db.ExecuteAsync("FT.CREATE", PinIndexName,
            "ON", "HASH",
            "PREFIX", "1", "pin:",
            "SCHEMA",
            "pinId", "TAG",
            "sessionId", "TAG",
            "type", "TAG",
            "content", "TEXT",
            "embedding", "VECTOR", "HNSW", "6",
            "TYPE", "FLOAT32",
            "DIM", _options.EmbeddingDim.ToString(),
            "DISTANCE_METRIC", "COSINE"
        );
    }
}
