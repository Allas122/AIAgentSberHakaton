using Domain.ValueTypes;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Review;

public interface IReviewQueue
{
    Task EnqueueAsync(ReviewJob job, CancellationToken ct = default);

    Task<QueuedReviewJob?> DequeueAsync(string consumer, CancellationToken ct = default);

    Task<QueuedReviewJob?> ReclaimAsync(string consumer, TimeSpan idleFor, CancellationToken ct = default);

    Task AcknowledgeAsync(string entryId);

    Task<long> PendingAsync();
}

public class ValkeyReviewQueue(IConnectionMultiplexer redis, ILogger<ValkeyReviewQueue> logger) : IReviewQueue
{
    public const string StreamKey = "review:jobs";
    public const string GroupName = "reviewers";

    public const int MaxAttempts = 3;

    private readonly IDatabase database = redis.GetDatabase();

    private bool _groupReady;

    public async Task EnqueueAsync(ReviewJob job, CancellationToken ct = default)
    {
        await EnsureGroupAsync();

        var entryId = await database.StreamAddAsync(StreamKey,
        [
            new NameValueEntry("chatId", job.ChatId.ToString()),
            new NameValueEntry("userId", job.UserId.ToString()),
            new NameValueEntry("manualId", job.ManualId.ToString()),
            new NameValueEntry("applicationId", job.ApplicationId.ToString()),
            new NameValueEntry("documentId", job.DocumentId?.ToString() ?? string.Empty),
            new NameValueEntry("storageKey", job.StorageKey),
            new NameValueEntry("fileName", job.FileName),
            new NameValueEntry("comment", job.Comment ?? string.Empty),
            new NameValueEntry("contestKind", job.ContestKind.ToString())
        ]);

        logger.LogInformation("Заявка {FileName} поставлена в очередь разбора: {EntryId}", job.FileName, entryId);
    }

    public async Task<QueuedReviewJob?> DequeueAsync(string consumer, CancellationToken ct = default)
    {
        await EnsureGroupAsync();

        var entries = await database.StreamReadGroupAsync(StreamKey, GroupName, consumer, ">", count: 1);

        return entries.Length == 0 ? null : Map(entries[0], attempt: 1);
    }

    public async Task<QueuedReviewJob?> ReclaimAsync(string consumer, TimeSpan idleFor, CancellationToken ct = default)
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
                "Заявка из записи {EntryId} не разобралась за {Attempts} попыток — снимаю с очереди",
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
            "Подбираю зависшую заявку {EntryId}, попытка {Attempt}", stale.MessageId, stale.DeliveryCount);

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

    private static QueuedReviewJob? Map(StreamEntry entry, int attempt)
    {
        if (entry.Values.Length == 0) return null;

        var fields = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());

        if (!Guid.TryParse(fields.GetValueOrDefault("chatId"), out var chatId)) return null;
        if (!Guid.TryParse(fields.GetValueOrDefault("userId"), out var userId)) return null;
        if (!Guid.TryParse(fields.GetValueOrDefault("manualId"), out var manualId)) return null;

        Guid.TryParse(fields.GetValueOrDefault("applicationId"), out var applicationId);
        Enum.TryParse<ContestKind>(fields.GetValueOrDefault("contestKind"), out var contestKind);

        var comment = fields.GetValueOrDefault("comment");

        var documentId = Guid.TryParse(fields.GetValueOrDefault("documentId"), out var parsedDocumentId)
            ? parsedDocumentId
            : (Guid?)null;

        var job = new ReviewJob(
            chatId,
            userId,
            manualId,
            applicationId,
            documentId,
            fields.GetValueOrDefault("storageKey", string.Empty),
            fields.GetValueOrDefault("fileName", "заявка.docx"),
            string.IsNullOrWhiteSpace(comment) ? null : comment,
            contestKind);

        return new QueuedReviewJob(entry.Id.ToString(), job, attempt);
    }
}
