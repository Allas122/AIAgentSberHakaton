using System.Runtime.InteropServices;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class PinRepository(
    IDatabase database,
    IEmbeddingClient embeddingClient,
    IOptions<ExpirationPolicyOption> exOptions,
    ILogger<PinRepository> logger) : IPinRepository
{
    private const int MaxEmbeddedChars = 2000;
    private const int DeleteBatchSize = 50;

    private readonly ExpirationPolicyOption _exOptions = exOptions.Value;

    private static string PinKey(Guid sessionId, Guid pinId) => $"pin:{sessionId}:{pinId}";
    private static string SessionPinsKey(Guid sessionId) => $"pins:{sessionId}";

    private async Task<T> DegradeAsync<T>(Func<Task<T>> action, T fallback, string operation)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (
            ex is RedisTimeoutException or RedisConnectionException or IOException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Заметки: операция {Operation} деградировала до значения по умолчанию", operation);
            return fallback;
        }
    }

    public Task<Guid> CreatePinAsync(Pin pin) =>
        DegradeAsync(() => CreatePinCoreAsync(pin), Guid.Empty, "CreatePin");

    public Task<Pin?> GetPinAsync(Guid sessionId, Guid pinId) =>
        DegradeAsync(() => GetPinCoreAsync(sessionId, pinId), null, "GetPin");

    public Task<bool> UpdatePinAsync(Pin pin) =>
        DegradeAsync(() => UpdatePinCoreAsync(pin), false, "UpdatePin");

    public Task<bool> DeletePinAsync(Guid sessionId, Guid pinId) =>
        DegradeAsync(() => DeletePinCoreAsync(sessionId, pinId), false, "DeletePin");

    public Task<int> DeletePinsAsync(Guid sessionId) =>
        DegradeAsync(() => DeletePinsCoreAsync(sessionId), 0, "DeletePins");

    public Task<IEnumerable<Pin>> GetPinsAsync(Guid sessionId, PinType? type = null) =>
        DegradeAsync(() => GetPinsCoreAsync(sessionId, type), [], "GetPins");

    public Task<IEnumerable<PinMatch>> KnnSearchPinsAsync(
        Guid sessionId,
        string query,
        int limit,
        PinType? type = null,
        double maxDistance = 2.0) =>
        DegradeAsync(
            () => KnnSearchPinsCoreAsync(sessionId, query, limit, type, maxDistance), [], "KnnSearchPins");

    private async Task<Guid> CreatePinCoreAsync(Pin pin)
    {
        var pinId = pin.Id == Guid.Empty ? Guid.NewGuid() : pin.Id;
        var createdAt = pin.CreatedAt == default ? DateTimeOffset.UtcNow : pin.CreatedAt;

        var pinKey = PinKey(pin.SessionId, pinId);
        var sessionPinsKey = SessionPinsKey(pin.SessionId);
        var ttl = TimeSpan.FromSeconds(_exOptions.PinExpirationSeconds);
        var embedding = await EmbedAsync(pin.Content);

        var transaction = database.CreateTransaction();
        _ = transaction.HashSetAsync(pinKey, [
            new HashEntry("pinId", pinId.ToString()),
            new HashEntry("sessionId", pin.SessionId.ToString()),
            new HashEntry("content", pin.Content),
            new HashEntry("type", pin.Type.ToString()),
            new HashEntry("createdAt", createdAt.ToUnixTimeMilliseconds()),
            new HashEntry("criterion", pin.CriterionIndex?.ToString() ?? string.Empty),
            new HashEntry("scope", pin.Scope.ToString()),
            new HashEntry("embedding", embedding)
        ]);
        _ = transaction.SortedSetAddAsync(sessionPinsKey, pinId.ToString(), createdAt.ToUnixTimeMilliseconds());
        _ = transaction.KeyExpireAsync(pinKey, ttl);
        _ = transaction.KeyExpireAsync(sessionPinsKey, ttl);

        var committed = await transaction.ExecuteAsync();
        if (!committed) throw new Exception("Failed to create pin in Valkey.");

        return pinId;
    }

    private async Task<Pin?> GetPinCoreAsync(Guid sessionId, Guid pinId)
    {
        var fields = await database.HashGetAllAsync(PinKey(sessionId, pinId));
        if (fields.Length == 0) return null;

        await SustainAsync(sessionId, [pinId]);

        return MapToPin(fields);
    }

    private async Task<bool> UpdatePinCoreAsync(Pin pin)
    {
        var pinKey = PinKey(pin.SessionId, pin.Id);
        if (!await database.KeyExistsAsync(pinKey)) return false;

        var embedding = await EmbedAsync(pin.Content);

        await database.HashSetAsync(pinKey, [
            new HashEntry("content", pin.Content),
            new HashEntry("type", pin.Type.ToString()),
            new HashEntry("criterion", pin.CriterionIndex?.ToString() ?? string.Empty),
            new HashEntry("scope", pin.Scope.ToString()),
            new HashEntry("embedding", embedding)
        ]);

        await SustainAsync(pin.SessionId, [pin.Id]);
        return true;
    }

    private async Task<bool> DeletePinCoreAsync(Guid sessionId, Guid pinId)
    {
        var transaction = database.CreateTransaction();
        var deleteTask = transaction.KeyDeleteAsync(PinKey(sessionId, pinId));
        _ = transaction.SortedSetRemoveAsync(SessionPinsKey(sessionId), pinId.ToString());

        await transaction.ExecuteAsync();
        return await deleteTask;
    }

    private async Task<int> DeletePinsCoreAsync(Guid sessionId)
    {
        var ids = await database.SortedSetRangeByScoreAsync(SessionPinsKey(sessionId));
        if (ids.Length == 0) return 0;

        var keys = ids
            .Select(raw => Guid.TryParse(raw.ToString(), out var pinId) ? PinKey(sessionId, pinId) : null)
            .Where(key => key is not null)
            .Select(key => (RedisKey)key!)
            .ToArray();

        foreach (var batch in keys.Chunk(DeleteBatchSize))
        {
            await UnlinkAsync(batch);
        }

        await UnlinkAsync([SessionPinsKey(sessionId)]);

        return keys.Length;
    }

    private async Task<IEnumerable<Pin>> GetPinsCoreAsync(Guid sessionId, PinType? type)
    {
        var ids = await database.SortedSetRangeByScoreAsync(SessionPinsKey(sessionId), order: Order.Ascending);
        if (ids.Length == 0) return [];

        var loaded = await LoadAsync(sessionId, ParseIds(ids));

        var pins = loaded.Values
            .Select(MapToPin)
            .Where(pin => type is null || pin.Type == type)
            .ToList();

        await SustainAsync(sessionId, loaded.Keys);

        return pins;
    }

    private async Task<IEnumerable<PinMatch>> KnnSearchPinsCoreAsync(
        Guid sessionId,
        string query,
        int limit,
        PinType? type,
        double maxDistance)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0) return [];

        var ids = await database.SortedSetRangeByScoreAsync(SessionPinsKey(sessionId), order: Order.Ascending);
        if (ids.Length == 0) return [];

        var queryVector = EmbeddingVector.FromBytes(await EmbedAsync(query));

        var loaded = await LoadAsync(sessionId, ParseIds(ids));

        var matches = new List<PinMatch>(loaded.Count);

        foreach (var fields in loaded.Values)
        {
            var pin = MapToPin(fields);
            if (type is not null && pin.Type != type) continue;

            var stored = fields.FirstOrDefault(field => field.Name == "embedding").Value;
            if (stored.IsNull) continue;

            var distance = EmbeddingVector.CosineDistance(queryVector, EmbeddingVector.FromBytes((byte[])stored!));
            if (distance <= maxDistance) matches.Add(new PinMatch(pin, distance));
        }

        await SustainAsync(sessionId, loaded.Keys);

        return matches
            .OrderBy(match => match.Distance)
            .Take(limit)
            .ToList();
    }

    private async Task<byte[]> EmbedAsync(string text)
    {
        var trimmed = text.Length <= MaxEmbeddedChars ? text : text[..MaxEmbeddedChars];
        var embedding = await embeddingClient.GetEmbeddingAsync(trimmed);

        return VectorToBytes(embedding);
    }

    private static byte[] VectorToBytes(IReadOnlyList<double> vector) => EmbeddingVector.ToBytes(vector);

    private async Task SustainAsync(Guid sessionId, IReadOnlyCollection<Guid> pinIds)
    {
        var ttl = TimeSpan.FromSeconds(_exOptions.PinExpirationSeconds);

        var batch = database.CreateBatch();
        var tasks = new List<Task>(pinIds.Count + 1)
        {
            batch.KeyExpireAsync(SessionPinsKey(sessionId), ttl)
        };

        tasks.AddRange(pinIds.Select(pinId => batch.KeyExpireAsync(PinKey(sessionId, pinId), ttl)));

        batch.Execute();

        await Task.WhenAll(tasks);
    }

    private async Task<Dictionary<Guid, HashEntry[]>> LoadAsync(Guid sessionId, IReadOnlyList<Guid> pinIds)
    {
        var batch = database.CreateBatch();
        var tasks = pinIds.ToDictionary(
            pinId => pinId,
            pinId => batch.HashGetAllAsync(PinKey(sessionId, pinId)));

        batch.Execute();

        await Task.WhenAll(tasks.Values);

        return tasks
            .Where(pair => pair.Value.Result.Length > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Result);
    }

    private static List<Guid> ParseIds(RedisValue[] ids) =>
        ids.Select(raw => Guid.TryParse(raw.ToString(), out var pinId) ? pinId : Guid.Empty)
            .Where(pinId => pinId != Guid.Empty)
            .ToList();

    private Task UnlinkAsync(RedisKey[] keys) =>
        keys.Length == 0
            ? Task.CompletedTask
            : database.ExecuteAsync("UNLINK", keys.Select(key => (object)key).ToArray());

    private static Pin MapToPin(HashEntry[] fields) =>
        MapToPin(fields.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString()));

    private static Pin MapToPin(Dictionary<string, string> dict)
    {
        Enum.TryParse<PinType>(dict.GetValueOrDefault("type"), ignoreCase: true, out var type);

        var createdAt = long.TryParse(dict.GetValueOrDefault("createdAt"), out var unixMs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
            : DateTimeOffset.UtcNow;

        var criterion = int.TryParse(dict.GetValueOrDefault("criterion"), out var index) && index > 0
            ? index
            : (int?)null;

        Enum.TryParse<FindingScope>(dict.GetValueOrDefault("scope"), ignoreCase: true, out var scope);

        return new Pin(
            Guid.Parse(dict["pinId"]),
            Guid.Parse(dict["sessionId"]),
            dict.GetValueOrDefault("content", string.Empty),
            type,
            createdAt,
            criterion,
            scope);
    }
}
