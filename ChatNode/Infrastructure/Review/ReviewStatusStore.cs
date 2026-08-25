using System.Text.Json;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Review;

public record ReviewStatusSnapshot(
    Guid DocumentId,
    Guid ChatId,
    string FileName,
    string Stage,
    string? Detail);

public interface IReviewStatusStore
{
    Task SaveAsync(Guid ownerId, ReviewStatusSnapshot snapshot);

    Task ClearAsync(Guid ownerId, Guid documentId);

    Task<IReadOnlyDictionary<Guid, ReviewStatusSnapshot>> ListAsync(Guid ownerId);
}

public class ValkeyReviewStatusStore(IConnectionMultiplexer redis) : IReviewStatusStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly IDatabase database = redis.GetDatabase();

    public async Task SaveAsync(Guid ownerId, ReviewStatusSnapshot snapshot)
    {
        var key = Key(ownerId);

        await database.HashSetAsync(key, snapshot.DocumentId.ToString(), JsonSerializer.Serialize(snapshot));
        await database.KeyExpireAsync(key, Lifetime);
    }

    public Task ClearAsync(Guid ownerId, Guid documentId)
        => database.HashDeleteAsync(Key(ownerId), documentId.ToString());

    public async Task<IReadOnlyDictionary<Guid, ReviewStatusSnapshot>> ListAsync(Guid ownerId)
    {
        var entries = await database.HashGetAllAsync(Key(ownerId));

        return entries
            .Select(entry => Deserialize(entry.Value))
            .OfType<ReviewStatusSnapshot>()
            .ToDictionary(snapshot => snapshot.DocumentId);
    }

    private static ReviewStatusSnapshot? Deserialize(RedisValue value)
    {
        if (value.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<ReviewStatusSnapshot>(value.ToString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Key(Guid ownerId) => $"review:status:{ownerId}";
}
