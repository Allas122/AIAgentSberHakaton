using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public class ValkeyIndexInitializer(
    IConnectionMultiplexer redis,
    ILogger<ValkeyIndexInitializer> logger,
    IOptions<GigaChatOptions> options)
    : IHostedService
{
    private const string ManualIndexName = "idx:manual_parts_v2";
    private const string ManualKeyPrefix = "manualpart:";
    private const string PinIndexName = "idx:pins";

    private GigaChatOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        await EnsureIndexAsync(db, ManualIndexName, CreateManualIndexAsync);
        await DropIndexAsync(db, PinIndexName);
    }

    private async Task DropIndexAsync(IDatabase db, string indexName)
    {
        try
        {
            await db.ExecuteAsync("FT.DROPINDEX", indexName);
            logger.LogWarning(
                "Индекс '{IndexName}' удалён: поиск по заметкам считается в памяти, "
                + "а HNSW вешает соединение на удалении ключей", indexName);
        }
        catch (RedisServerException ex) when (IsIndexMissing(ex))
        {
        }
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
        catch (RedisServerException ex) when (IsIndexMissing(ex))
        {
            await create(db);
            logger.LogInformation("Index '{IndexName}' successfully created.", indexName);
        }
    }

    private static bool IsIndexMissing(RedisServerException ex) =>
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unknown index", StringComparison.OrdinalIgnoreCase);

    private async Task CreateManualIndexAsync(IDatabase db)
    {
        await db.ExecuteAsync("FT.CREATE", ManualIndexName,
            "ON", "HASH",
            "PREFIX", "1", ManualKeyPrefix,
            "SCHEMA",
            "partId", "TAG",
            "manualId", "TAG",
            "embedding", "VECTOR", "HNSW", "6",
            "TYPE", "FLOAT32",
            "DIM", _options.EmbeddingDim.ToString(),
            "DISTANCE_METRIC", "COSINE"
        );
    }
}

