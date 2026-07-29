using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

public class ValkeyIndexInitializer(
    IConnectionMultiplexer redis,
    ILogger<ValkeyIndexInitializer> logger,
    IOptions<GigaChatOptions> options)
    : IHostedService
{
    private const string IndexName = "idx:manual_parts";
    private GigaChatOptions _options = options.Value;
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();

        try
        {
            await db.ExecuteAsync("FT.INFO", IndexName);
        }
        catch (RedisServerException ex) when (ex.Message.Contains(
                                                  "Index with name 'idx:manual_parts' not found in database 0"))
        {
            await CreateIndexAsync(db);
            logger.Log(LogLevel.Information, $" Index '{IndexName}' successfully created.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task CreateIndexAsync(IDatabase db)
    {
        string VectorDim = _options.EmbeddingDim.ToString(); 

        await db.ExecuteAsync("FT.CREATE", IndexName,
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
            "DIM", VectorDim,
            "DISTANCE_METRIC", "COSINE"
        );
    }
}