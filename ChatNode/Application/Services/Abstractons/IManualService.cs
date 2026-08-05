using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface IManualService
{
    public Task<string> ProcessManual(string title,Stream fileStream, CancellationToken cancellationToken);
    public Task<IEnumerable<ManualDto>> GetManuals();
}