using Domain.Entities;
using Domain.ValueTypes;

namespace Domain.Repositories;

public interface IDatasetRepository
{
    Task SaveAsync(Dataset dataset, CancellationToken ct = default);

    Task<IReadOnlyList<Dataset>> ListAsync(ManualScope? scope = null, CancellationToken ct = default);

    Task<Dataset?> GetAsync(Guid datasetId, CancellationToken ct = default);

    Task<IReadOnlyList<DatasetRow>> SampleRowsAsync(Guid datasetId, int count, CancellationToken ct = default);

    Task DeleteByManualAsync(Guid manualId, CancellationToken ct = default);
}
