using System.Runtime.InteropServices;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class PinRepository(
    IDatabase database,
    IEmbeddingClient embeddingClient,
    IOptions<ExpirationPolicyOption> exOptions) : IPinRepository
{
    private const string IndexName = "idx:pins";
    private const int MaxEmbeddedChars = 2000;

    private readonly ExpirationPolicyOption _exOptions = exOptions.Value;

    private static string PinKey(Guid sessionId, Guid pinId) => $"pin:{sessionId}:{pinId}";
    private static string SessionPinsKey(Guid sessionId) => $"pins:{sessionId}";

    public async Task<Guid> CreatePinAsync(Pin pin)
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
            new HashEntry("embedding", embedding)
        ]);
        _ = transaction.SortedSetAddAsync(sessionPinsKey, pinId.ToString(), createdAt.ToUnixTimeMilliseconds());
        _ = transaction.KeyExpireAsync(pinKey, ttl);
        _ = transaction.KeyExpireAsync(sessionPinsKey, ttl);

        var committed = await transaction.ExecuteAsync();
        if (!committed) throw new Exception("Failed to create pin in Valkey.");

        return pinId;
    }

    public async Task<Pin?> GetPinAsync(Guid sessionId, Guid pinId)
    {
        var fields = await database.HashGetAllAsync(PinKey(sessionId, pinId));
        if (fields.Length == 0) return null;

        await SustainAsync(sessionId, [pinId]);

        return MapToPin(fields);
    }

    public async Task<bool> UpdatePinAsync(Pin pin)
    {
        var pinKey = PinKey(pin.SessionId, pin.Id);
        if (!await database.KeyExistsAsync(pinKey)) return false;

        var embedding = await EmbedAsync(pin.Content);

        await database.HashSetAsync(pinKey, [
            new HashEntry("content", pin.Content),
            new HashEntry("type", pin.Type.ToString()),
            new HashEntry("embedding", embedding)
        ]);

        await SustainAsync(pin.SessionId, [pin.Id]);
        return true;
    }

    public async Task<bool> DeletePinAsync(Guid sessionId, Guid pinId)
    {
        var transaction = database.CreateTransaction();
        var deleteTask = transaction.KeyDeleteAsync(PinKey(sessionId, pinId));
        _ = transaction.SortedSetRemoveAsync(SessionPinsKey(sessionId), pinId.ToString());

        await transaction.ExecuteAsync();
        return await deleteTask;
    }

    public async Task<IEnumerable<Pin>> GetPinsAsync(Guid sessionId, PinType? type = null)
    {
        var ids = await database.SortedSetRangeByScoreAsync(SessionPinsKey(sessionId), order: Order.Ascending);
        if (ids.Length == 0) return [];

        var pins = new List<Pin>(ids.Length);
        var alivePinIds = new List<Guid>(ids.Length);

        foreach (var idRaw in ids)
        {
            if (!Guid.TryParse(idRaw.ToString(), out var pinId)) continue;

            var fields = await database.HashGetAllAsync(PinKey(sessionId, pinId));
            if (fields.Length == 0) continue;

            alivePinIds.Add(pinId);

            var pin = MapToPin(fields);
            if (type is null || pin.Type == type) pins.Add(pin);
        }

        await SustainAsync(sessionId, alivePinIds);

        return pins;
    }

    public async Task<IEnumerable<PinMatch>> KnnSearchPinsAsync(
        Guid sessionId,
        string query,
        int limit,
        PinType? type = null,
        double maxDistance = 2.0)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0) return [];

        var vectorBytes = await EmbedAsync(query);

        var filter = type is null
            ? $"@sessionId:{{{EscapeTag(sessionId.ToString())}}}"
            : $"@sessionId:{{{EscapeTag(sessionId.ToString())}}} @type:{{{type}}}";

        try
        {
            var results = await database.ExecuteAsync("FT.SEARCH", IndexName,
                $"({filter})=>[KNN {limit} @embedding $vec AS score]",
                "PARAMS", "2", "vec", vectorBytes,
                "DIALECT", "2");

            return ParseSearchResponse(results)
                .Where(match => match.Distance <= maxDistance)
                .OrderBy(match => match.Distance)
                .ToList();
        }
        catch (RedisServerException ex)
        {
            Console.WriteLine($"[PIN KNN FAILED]: {ex.Message}");
            return [];
        }
    }

    private async Task<byte[]> EmbedAsync(string text)
    {
        var trimmed = text.Length <= MaxEmbeddedChars ? text : text[..MaxEmbeddedChars];
        var embedding = await embeddingClient.GetEmbeddingAsync(trimmed);

        return VectorToBytes(embedding);
    }

    private static byte[] VectorToBytes(IReadOnlyList<double> vector)
    {
        var floatArray = vector.Select(x => (float)x).ToArray();
        return MemoryMarshal.AsBytes(floatArray.AsSpan()).ToArray();
    }

    private static string EscapeTag(string value) => value.Replace("-", "\\-");

    private Task SustainAsync(Guid sessionId, IReadOnlyCollection<Guid> pinIds)
    {
        var ttl = TimeSpan.FromSeconds(_exOptions.PinExpirationSeconds);

        var batch = database.CreateBatch();
        _ = batch.KeyExpireAsync(SessionPinsKey(sessionId), ttl);
        foreach (var pinId in pinIds)
        {
            _ = batch.KeyExpireAsync(PinKey(sessionId, pinId), ttl);
        }

        batch.Execute();
        return Task.CompletedTask;
    }

    private static IEnumerable<PinMatch> ParseSearchResponse(RedisResult result)
    {
        var matches = new List<PinMatch>();
        var rows = (RedisResult[])result!;

        if (rows.Length <= 1) return matches;

        for (var i = 1; i < rows.Length; i += 2)
        {
            var fields = (RedisResult[])rows[i + 1]!;
            var dict = new Dictionary<string, string>();

            for (var j = 0; j < fields.Length; j += 2)
            {
                dict[fields[j].ToString()!] = fields[j + 1].ToString()!;
            }

            if (!dict.ContainsKey("pinId")) continue;

            var distance = double.TryParse(
                dict.GetValueOrDefault("score"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 2.0;

            matches.Add(new PinMatch(MapToPin(dict), distance));
        }

        return matches;
    }

    private static Pin MapToPin(HashEntry[] fields) =>
        MapToPin(fields.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString()));

    private static Pin MapToPin(Dictionary<string, string> dict)
    {
        Enum.TryParse<PinType>(dict.GetValueOrDefault("type"), ignoreCase: true, out var type);

        var createdAt = long.TryParse(dict.GetValueOrDefault("createdAt"), out var unixMs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
            : DateTimeOffset.UtcNow;

        return new Pin(
            Guid.Parse(dict["pinId"]),
            Guid.Parse(dict["sessionId"]),
            dict.GetValueOrDefault("content", string.Empty),
            type,
            createdAt);
    }
}
