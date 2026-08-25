using StackExchange.Redis;

namespace ChatNode.Infrastructure.Manuals;

public interface IManualQueue
{
    Task EnqueueAsync(ManualJob job, CancellationToken ct = default);

    Task<QueuedManualJob?> DequeueAsync(string consumer, CancellationToken ct = default);

    Task<QueuedManualJob?> ReclaimAsync(string consumer, TimeSpan idleFor, CancellationToken ct = default);

    Task AcknowledgeAsync(string entryId);

    Task<long> PendingAsync();
}

public class ValkeyManualQueue(IConnectionMultiplexer redis, ILogger<ValkeyManualQueue> logger) : IManualQueue
{
    public const string StreamKey = "manual:jobs";
    public const string GroupName = "manual-parsers";

    public const int MaxAttempts = 2;

    private readonly IDatabase database = redis.GetDatabase();

    private bool _groupReady;

    public async Task EnqueueAsync(ManualJob job, CancellationToken ct = default)
    {
        await EnsureGroupAsync();

        var entryId = await database.StreamAddAsync(StreamKey,
        [
            new NameValueEntry("manualId", job.ManualId.ToString()),
            new NameValueEntry("ownerId", job.OwnerId.ToString()),
            new NameValueEntry("title", job.Title),
            new NameValueEntry("storageKey", job.StorageKey),
            new NameValueEntry("fileName", job.FileName)
        ]);

        logger.LogInformation("Методичка {Title} поставлена в очередь разбора: {EntryId}", job.Title, entryId);
    }

    public async Task<QueuedManualJob?> DequeueAsync(string consumer, CancellationToken ct = default)
    {
        await EnsureGroupAsync();

        var entries = await database.StreamReadGroupAsync(StreamKey, GroupName, consumer, ">", count: 1);

        return entries.Length == 0 ? null : Map(entries[0], attempt: 1);
    }

    public async Task<QueuedManualJob?> ReclaimAsync(string consumer, TimeSpan idleFor, CancellationToken ct = default)
    {
        await EnsureGroupAsync();

        var pending = await database.StreamPendingMessagesAsync(
            StreamKey, GroupName, count: 1, consumerName: RedisValue.Null, minId: "-");

        if (pending.Length == 0) return null;

        var stale = pending[0];
        if (stale.IdleTimeInMilliseconds < idleFor.TotalMilliseconds) return null;

        if (stale.DeliveryCount > MaxAttempts)
        {
            logger.LogError(
                "Методичка из записи {EntryId} не разобралась за {Attempts} попыток — снимаю с очереди",
                stale.MessageId, stale.DeliveryCount);

            await AcknowledgeAsync(stale.MessageId!);
            return null;
        }

        var claimed = await database.StreamClaimAsync(
            StreamKey, GroupName, consumer, (long)idleFor.TotalMilliseconds, [stale.MessageId]);

        if (claimed.Length == 0 || claimed[0].Values.Length == 0)
        {
            await AcknowledgeAsync(stale.MessageId!);
            return null;
        }

        logger.LogWarning(
            "Подбираю зависшую методичку {EntryId}, попытка {Attempt}", stale.MessageId, stale.DeliveryCount);

        return Map(claimed[0], stale.DeliveryCount);
    }

    public async Task AcknowledgeAsync(string entryId)
    {
        await database.StreamAcknowledgeAsync(StreamKey, GroupName, entryId);
        await database.StreamDeleteAsync(StreamKey, [entryId]);
    }

    public async Task<long> PendingAsync()
    {
        await EnsureGroupAsync();
        return await database.StreamLengthAsync(StreamKey);
    }

    private async Task EnsureGroupAsync()
    {
        if (_groupReady) return;

        try
        {
            await database.StreamCreateConsumerGroupAsync(StreamKey, GroupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
        }

        _groupReady = true;
    }

    private static QueuedManualJob? Map(StreamEntry entry, int attempt)
    {
        if (entry.Values.Length == 0) return null;

        var fields = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());

        if (!Guid.TryParse(fields.GetValueOrDefault("manualId"), out var manualId)) return null;
        if (!Guid.TryParse(fields.GetValueOrDefault("ownerId"), out var ownerId)) return null;

        var job = new ManualJob(
            manualId,
            ownerId,
            fields.GetValueOrDefault("title", "Методичка"),
            fields.GetValueOrDefault("storageKey", string.Empty),
            fields.GetValueOrDefault("fileName", "положение.md"));

        return new QueuedManualJob(entry.Id.ToString(), job, attempt);
    }
}
