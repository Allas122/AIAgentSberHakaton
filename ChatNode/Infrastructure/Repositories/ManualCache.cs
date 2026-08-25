using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Database.Entities;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Repositories;

public class ManualCache(
    IDatabase database,
    IOptions<CachePolicyOption> options,
    ILogger<ManualCache> logger)
{
    public const string IndexName = "idx:manual_parts_v2";
    public const string KeyPrefix = "manualpart:";

    private readonly CachePolicyOption _policy = options.Value;

    public static string PartKey(Guid manualId, Guid partId) => $"{KeyPrefix}{manualId}:{partId}";

    private static string WarmKey(Guid manualId) => $"manualcache:{manualId}";

    public async Task<bool> IsWarmAsync(Guid manualId) => await database.KeyExistsAsync(WarmKey(manualId));

    public async Task WarmAsync(Guid manualId, IReadOnlyList<StoredManualPart> parts)
    {
        var ttl = TimeSpan.FromSeconds(_policy.ManualPartsSeconds);

        var batch = database.CreateBatch();
        var tasks = new List<Task>(parts.Count * 2 + 2);

        foreach (var part in parts)
        {
            var key = PartKey(part.ManualId, part.Id);

            tasks.Add(batch.HashSetAsync(key, ToEntries(part)));
            tasks.Add(batch.KeyExpireAsync(key, ttl));
        }

        tasks.Add(batch.StringSetAsync(WarmKey(manualId), parts.Count, ttl));

        batch.Execute();
        await Task.WhenAll(tasks);

        logger.LogInformation("Кеш методички {ManualId} прогрет: {Count} частей", manualId, parts.Count);
    }

    public async Task PutAsync(StoredManualPart part)
    {
        var key = PartKey(part.ManualId, part.Id);
        var ttl = TimeSpan.FromSeconds(_policy.ManualPartsSeconds);

        await database.HashSetAsync(key, ToEntries(part));
        await database.KeyExpireAsync(key, ttl);
    }

    public const int DeleteBatchSize = 50;

    public async Task EvictAsync(Guid manualId, IEnumerable<Guid> partIds)
    {
        var keys = partIds.Select(id => (RedisKey)PartKey(manualId, id)).Append(WarmKey(manualId)).ToArray();

        foreach (var batch in keys.Chunk(DeleteBatchSize))
        {
            await database.KeyDeleteAsync(batch);
        }
    }

    public async Task EvictPartAsync(Guid manualId, Guid partId)
    {
        await database.KeyDeleteAsync(PartKey(manualId, partId));
        await database.KeyDeleteAsync(WarmKey(manualId));
    }

    private static HashEntry[] ToEntries(StoredManualPart part) =>
    [
        new("partId", part.Id.ToString()),
        new("manualId", part.ManualId.ToString()),
        new("title", part.Title),
        new("navigation", part.Navigation),
        new("content", part.Content),
        new("embedding", part.Embedding)
    ];
}
