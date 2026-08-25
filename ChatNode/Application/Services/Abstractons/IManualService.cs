using ChatNode.Application.DTO;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Manuals;

namespace ChatNode.Application.Services.Abstractons;

public interface IManualService
{
    public Task<ManualUploadDto> QueueManualAsync(
        string title,
        Guid ownerId,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken);

    public Task<ManualParseReport> ParseManualAsync(
        ManualJob job,
        Func<ManualParseProgress, Task> onProgress,
        CancellationToken cancellationToken);

    public Task<IEnumerable<ManualDto>> GetManuals();

    public Task<ManualDto?> GetManual(Guid id);
}
