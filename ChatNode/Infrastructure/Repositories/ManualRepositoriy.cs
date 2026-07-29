using System.Runtime.InteropServices;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class ManualRepository(
    IDatabase database,
    IConnectionMultiplexer redis,
    IEmbeddingClient embeddingClient
) : IManualRepository
{
    private const string IndexName = "idx:manual_parts";

    public async Task<Guid> CreateManualAsync(Manual manual)
    {
        var manualId = manual.Id == Guid.Empty ? Guid.NewGuid() : manual.Id;
        var key = $"manual:{manualId}";
        await database.HashSetAsync(key, [
            new HashEntry("title", manual.Title),
            new HashEntry("navigation", manual.Navigation)
        ]);
        return manualId;
    }

    public async Task<Manual?> GetManualAsync(Guid id)
    {
        var values = await database.HashGetAllAsync($"manual:{id}");
        if (values.Length == 0) return null;

        var dict = values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return new Manual(id, dict["title"], dict["navigation"]);
    }

    public async Task UpdateManualAsync(Manual manual)
    {
        await CreateManualAsync(manual);
    }

    public async Task DeleteManualAsync(Guid id)
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(database.Database, $"manual:{id}*").ToArray();
        if (keys.Length > 0) await database.KeyDeleteAsync(keys);
    }

    public async Task<Guid> CreateManualPartAsync(ManualPart part)
    {
        var textToEmbed = $"{part.Title} {part.Navigation} {part.Content}";

        if (textToEmbed.Length > 1200) 
        {
            textToEmbed = textToEmbed.Substring(0, 1200);
        }

        var embedding = await embeddingClient.GetEmbeddingAsync(textToEmbed);
    
        var partId = part.Id == Guid.Empty ? Guid.NewGuid() : part.Id;
        var key = $"manual:{part.ManualId}:part:{partId}";

        await database.HashSetAsync(key, [
            new HashEntry("partId", partId.ToString()),
            new HashEntry("manualId", part.ManualId.ToString()),
            new HashEntry("title", part.Title),
            new HashEntry("navigation", part.Navigation),
            new HashEntry("content", part.Content),
            new HashEntry("embedding", VectorToBytes(embedding)) 
        ]);

        return partId;
    }

    public async Task<ManualPart?> GetManualPartAsync(Guid manualId, Guid partId)
    {
        var key = $"manual:{manualId}:part:{partId}";
        var values = await database.HashGetAllAsync(key);

        if (values.Length == 0) return null;

        var dict = values.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
        return MapToPart(dict);
    }

    public async Task DeleteManualPartAsync(Guid id)
    {
        var result = await database.ExecuteAsync("FT.SEARCH", IndexName, $"@partId:{{{id}}}", "NOCONTENT");
        var rows = (RedisResult[])result!;
        if (rows.Length > 1) 
        {
            await database.KeyDeleteAsync(rows[1].ToString());
        }
    }

    public async Task<IEnumerable<ManualPart>> KnnSearchManualPartAsync(string searchTerm, int limit, Guid? manualId = null)
    {
        var embedding = await embeddingClient.GetEmbeddingAsync(searchTerm);
        var vectorBytes = VectorToBytes(embedding);

        var filter = manualId.HasValue
            ? $"@manualId:{{{manualId.Value}}}"
            : "*";

        var results = await database.ExecuteAsync("FT.SEARCH", IndexName,
            $"{filter}=>[KNN {limit} @embedding $vec AS score]",
            "PARAMS", "2", "vec", vectorBytes,
            "DIALECT", "2");

        return ParseSearchResponse(results);
    }

    private byte[] VectorToBytes(IReadOnlyList<double> vector)
    {
        float[] floatArray = vector.Select(x => (float)x).ToArray();
        return MemoryMarshal.AsBytes(floatArray.AsSpan()).ToArray();
    }

    private ManualPart MapToPart(Dictionary<string, string> dict)
    {
        return new ManualPart(
            Guid.Parse(dict["partId"]),
            Guid.Parse(dict["manualId"]),
            dict.GetValueOrDefault("title", "Без названия"),
            Navigation: dict.GetValueOrDefault("navigation", ""),
            Content: dict.GetValueOrDefault("content", "")
        );
    }

    private IEnumerable<ManualPart> ParseSearchResponse(RedisResult result)
    {
        var list = new List<ManualPart>();
        var rows = (RedisResult[])result!;
        
        if (rows.Length <= 1) return list;

        for (var i = 1; i < rows.Length; i += 2)
        {
            var fields = (RedisResult[])rows[i + 1]!;
            var dict = new Dictionary<string, string>();
            
            for (var j = 0; j < fields.Length; j += 2)
            {
                var key = fields[j].ToString()!;
                var value = fields[j + 1].ToString()!;
                dict[key] = value;
            }
            
            list.Add(MapToPart(dict));
        }

        return list;
    }
}