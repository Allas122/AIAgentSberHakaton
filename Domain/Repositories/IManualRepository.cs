using Domain.Entities;

namespace Domain.Repositories;

public interface IManualRepository
{
    public Task<Guid> CreateManualAsync(Manual manual);
    public Task DeleteManualAsync(Guid id);
    public Task<Manual?> GetManualAsync(Guid id);
    public Task UpdateManualAsync(Manual manual);
    public Task<bool> SetManualStatusAsync(ManualStatus status);

    public Task<Guid> CreateManualPartAsync(ManualPart manual);
    public Task DeleteManualPartAsync(Guid id);
    public Task<ManualPart?> GetManualPartAsync(Guid manualId, Guid partId);

    public Task<IEnumerable<ManualPart>> KnnSearchManualPartAsync(string searchTerm, int limit, Guid? manualId = null);
    public Task<IEnumerable<Guid>> GetManualIdsAsync();
    public Task<IReadOnlyList<Manual>> GetManualsAsync();
}