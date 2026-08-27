using ChatNode.Infrastructure.Database;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class DatasetRepository(AppDbContext context) : IDatasetRepository
{
    public async Task SaveAsync(Dataset dataset, CancellationToken ct = default)
    {
        context.Datasets.Add(dataset);

        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Dataset>> ListAsync(
        ManualScope? scope = null,
        CancellationToken ct = default)
    {
        var datasets = context.Datasets.AsNoTracking().Include(dataset => dataset.Columns);

        var filtered = scope.HasValue
            ? datasets.Where(dataset => context.Manuals
                .Any(manual => manual.Id == dataset.ManualId && manual.Scope == scope.Value))
            : datasets;

        return await filtered
            .OrderByDescending(dataset => dataset.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Dataset?> GetAsync(Guid datasetId, CancellationToken ct = default) =>
        await context.Datasets
            .AsNoTracking()
            .Include(dataset => dataset.Columns)
            .FirstOrDefaultAsync(dataset => dataset.Id == datasetId, ct);

    public async Task<IReadOnlyList<DatasetRow>> SampleRowsAsync(
        Guid datasetId,
        int count,
        CancellationToken ct = default) =>
        await context.DatasetRows
            .AsNoTracking()
            .Where(row => row.DatasetId == datasetId)
            .OrderBy(row => row.Ordinal)
            .Take(count)
            .ToListAsync(ct);

    public async Task DeleteByManualAsync(Guid manualId, CancellationToken ct = default)
    {
        var datasets = await context.Datasets
            .Where(dataset => dataset.ManualId == manualId)
            .ToListAsync(ct);

        if (datasets.Count == 0) return;

        context.Datasets.RemoveRange(datasets);

        await context.SaveChangesAsync(ct);
    }
}
