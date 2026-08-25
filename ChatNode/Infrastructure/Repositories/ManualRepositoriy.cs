using System.Runtime.InteropServices;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Database.Configurations;
using ChatNode.Infrastructure.Database.Entities;
using ChatNode.Infrastructure.Mappers;
using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Repositories;

public class ManualRepository(
    IDatabase database,
    AppDbContext context,
    ManualCache cache,
    IEmbeddingClient embeddingClient,
    ILogger<ManualRepository> logger
) : IManualRepository
{
    private const int MaxEmbeddedChars = 2000;

    public async Task<Guid> CreateManualAsync(Manual manual)
    {
        var manualId = manual.Id == Guid.Empty ? Guid.NewGuid() : manual.Id;
        var now = DateTimeOffset.UtcNow;

        var stored = await context.Manuals.FirstOrDefaultAsync(x => x.Id == manualId);

        if (stored is null)
        {
            context.Manuals.Add(new StoredManual
            {
                Id = manualId,
                Title = manual.Title,
                Navigation = manual.Navigation,
                Stage = manual.Stage,
                TotalChunks = manual.TotalChunks,
                ProcessedChunks = manual.ProcessedChunks,
                FailedChunks = manual.FailedChunks,
                StatusDetail = manual.StatusDetail,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            stored.Title = manual.Title;
            stored.Navigation = manual.Navigation;
            stored.UpdatedAt = now;
        }

        await context.SaveChangesAsync();

        return manualId;
    }

    public async Task<Manual?> GetManualAsync(Guid id)
    {
        var stored = await context.Manuals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

        return stored?.MapToManual();
    }

    public async Task UpdateManualAsync(Manual manual)
    {
        await CreateManualAsync(manual);
    }

    public async Task<bool> SetManualStatusAsync(ManualStatus status)
    {
        var stored = await context.Manuals.FirstOrDefaultAsync(x => x.Id == status.ManualId);
        if (stored is null) return false;

        stored.Stage = status.Stage;
        stored.TotalChunks = status.TotalChunks;
        stored.ProcessedChunks = status.ProcessedChunks;
        stored.FailedChunks = status.FailedChunks;
        stored.StatusDetail = Trim(status.Detail);
        stored.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();

        return true;
    }

    private static string? Trim(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return null;

        return detail.Length <= ManualConfiguration.MaxStatusDetailLength
            ? detail
            : detail[..ManualConfiguration.MaxStatusDetailLength];
    }

    public async Task DeleteManualAsync(Guid id)
    {
        var partIds = await context.ManualParts
            .Where(x => x.ManualId == id)
            .Select(x => x.Id)
            .ToListAsync();

        await context.Manuals.Where(x => x.Id == id).ExecuteDeleteAsync();
        await cache.EvictAsync(id, partIds);
    }

    public async Task<Guid> CreateManualPartAsync(ManualPart part)
    {
        var textToEmbed = $"{part.Title} {part.Navigation} {part.Content}";
        if (textToEmbed.Length > MaxEmbeddedChars) textToEmbed = textToEmbed[..MaxEmbeddedChars];

        var embedding = await embeddingClient.GetEmbeddingAsync(textToEmbed);

        var stored = new StoredManualPart
        {
            Id = part.Id == Guid.Empty ? Guid.NewGuid() : part.Id,
            ManualId = part.ManualId,
            Title = part.Title,
            Navigation = part.Navigation,
            Content = part.Content,
            Embedding = VectorToBytes(embedding),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.ManualParts.Add(stored);
        await context.SaveChangesAsync();

        await cache.PutAsync(stored);

        return stored.Id;
    }

    public async Task<ManualPart?> GetManualPartAsync(Guid manualId, Guid partId)
    {
        var stored = await context.ManualParts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partId && x.ManualId == manualId);

        return stored?.MapToManualPart();
    }

    public async Task DeleteManualPartAsync(Guid id)
    {
        var stored = await context.ManualParts.FirstOrDefaultAsync(x => x.Id == id);
        if (stored is null) return;

        var manualId = stored.ManualId;

        context.ManualParts.Remove(stored);
        await context.SaveChangesAsync();

        await cache.EvictPartAsync(manualId, id);
    }

    public async Task<IEnumerable<ManualPart>> KnnSearchManualPartAsync(
        string searchTerm,
        int limit,
        Guid? manualId = null)
    {
        await EnsureWarmAsync(manualId);

        var embedding = await embeddingClient.GetEmbeddingAsync(searchTerm);
        var vectorBytes = VectorToBytes(embedding);

        var filter = manualId.HasValue
            ? $"@manualId:{{{EscapeTag(manualId.Value)}}}"
            : "*";

        try
        {
            var results = await database.ExecuteAsync("FT.SEARCH", ManualCache.IndexName,
                $"{filter}=>[KNN {limit} @embedding $vec AS score]",
                "PARAMS", "2", "vec", vectorBytes,
                "LIMIT", "0", limit.ToString(),
                "DIALECT", "2");

            return ParseSearchResponse(results);
        }
        catch (RedisServerException ex)
        {
            logger.LogError(ex, "Поиск по методичке {ManualId} не выполнен", manualId);
            return [];
        }
    }

    public async Task<IReadOnlyList<Manual>> GetManualsAsync()
    {
        var stored = await context.Manuals
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return stored.Select(x => x.MapToManual()).ToList();
    }

    public async Task<IEnumerable<Guid>> GetManualIdsAsync() =>
        await context.Manuals
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync();

    private async Task EnsureWarmAsync(Guid? manualId)
    {
        if (manualId is null)
        {
            var all = await context.Manuals.AsNoTracking().Select(x => x.Id).ToListAsync();
            foreach (var id in all) await EnsureWarmAsync(id);
            return;
        }

        if (await cache.IsWarmAsync(manualId.Value)) return;

        var parts = await context.ManualParts
            .AsNoTracking()
            .Where(x => x.ManualId == manualId.Value)
            .ToListAsync();

        if (parts.Count == 0) return;

        await cache.WarmAsync(manualId.Value, parts);
    }

    private static string EscapeTag(Guid value) => value.ToString().Replace("-", "\\-");

    private static byte[] VectorToBytes(IReadOnlyList<double> vector) => EmbeddingVector.ToBytes(vector);

    private static IEnumerable<ManualPart> ParseSearchResponse(RedisResult result)
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
                dict[fields[j].ToString()!] = fields[j + 1].ToString()!;
            }

            var part = MapToPart(dict);
            if (part is not null) list.Add(part);
        }

        return list;
    }

    private static ManualPart? MapToPart(Dictionary<string, string> dict)
    {
        if (!Guid.TryParse(dict.GetValueOrDefault("partId"), out var partId)) return null;
        if (!Guid.TryParse(dict.GetValueOrDefault("manualId"), out var manualId)) return null;

        return new ManualPart(
            partId,
            manualId,
            dict.GetValueOrDefault("title", "Без названия"),
            Navigation: dict.GetValueOrDefault("navigation", ""),
            Content: dict.GetValueOrDefault("content", "")
        );
    }
}
