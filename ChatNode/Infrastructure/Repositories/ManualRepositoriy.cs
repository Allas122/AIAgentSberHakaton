using System.Runtime.InteropServices;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Database.Configurations;
using ChatNode.Infrastructure.Database.Entities;
using ChatNode.Infrastructure.Mappers;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class ManualRepository(
    AppDbContext context,
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
                Scope = manual.Scope,
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
        await context.Manuals.Where(x => x.Id == id).ExecuteDeleteAsync();
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
            EmbeddingVector = ToVector(embedding),
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.ManualParts.Add(stored);
        await context.SaveChangesAsync();

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

        context.ManualParts.Remove(stored);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ManualPart>> KnnSearchManualPartAsync(
        string searchTerm,
        int limit,
        Guid? manualId = null)
    {
        var embedding = await embeddingClient.GetEmbeddingAsync(searchTerm);
        var query = ToVector(embedding);

        var parts = context.ManualParts.AsNoTracking().Where(part => part.EmbeddingVector != null);

        if (manualId.HasValue) parts = parts.Where(part => part.ManualId == manualId.Value);

        var found = await parts
            .OrderBy(part => part.EmbeddingVector!.CosineDistance(query))
            .Take(limit)
            .ToListAsync();

        logger.LogDebug(
            "Методичка {ManualId}: KNN по pgvector -> {Found} частей",
            manualId,
            found.Count);

        return found.Select(part => part.MapToManualPart()).ToList();
    }

    public async Task<IReadOnlyList<Manual>> GetManualsAsync(ManualScope? scope = null)
    {
        var manuals = context.Manuals.AsNoTracking();

        if (scope.HasValue) manuals = manuals.Where(x => x.Scope == scope.Value);

        var stored = await manuals
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

    private static byte[] VectorToBytes(IReadOnlyList<double> vector) => EmbeddingVector.ToBytes(vector);

    private static Vector ToVector(IReadOnlyList<double> vector) => new(EmbeddingVector.ToFloats(vector));
}
