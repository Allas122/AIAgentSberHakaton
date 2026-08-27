using ChatNode.Application.DTO;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Manuals;
using Domain.ValueTypes;

namespace ChatNode.Application.Services.Abstractons;

public interface IManualService
{
    public Task<ManualUploadDto> QueueManualAsync(
        string title,
        Guid ownerId,
        Stream fileStream,
        string fileName,
        ManualScope scope,
        CancellationToken cancellationToken);

    public Task<ManualParseReport> ParseManualAsync(
        ManualJob job,
        Func<ManualParseProgress, Task> onProgress,
        CancellationToken cancellationToken);

    public Task<IEnumerable<ManualDto>> GetManuals(bool includeStaff);

    public Task<ManualDto?> GetManual(Guid id, bool includeStaff);
}
