using StackExchange.Redis;

public class ValkeyIndexInitializer(
    IConnectionMultiplexer redis,
    ILogger<ValkeyIndexInitializer> logger)
    : IHostedService
{
    private static readonly string[] LegacyIndexes = ["idx:manual_parts_v2", "idx:pins"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        foreach (var indexName in LegacyIndexes)
        {
            await DropIndexAsync(db, indexName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task DropIndexAsync(IDatabase db, string indexName)
    {
        try
        {
            await db.ExecuteAsync("FT.DROPINDEX", indexName);

            logger.LogWarning(
                "Индекс '{IndexName}' удалён: векторный поиск переехал в PostgreSQL (pgvector), "
                + "а HNSW в Valkey вешал соединение на удалении ключей",
                indexName);
        }
        catch (RedisServerException ex) when (IsNothingToDrop(ex))
        {
        }
    }

    private static bool IsNothingToDrop(RedisServerException ex) =>
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unknown index", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unknown command", StringComparison.OrdinalIgnoreCase);
}
