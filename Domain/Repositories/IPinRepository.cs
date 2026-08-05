using Domain.Entities;

namespace Domain.Repositories;

public interface IPinRepository
{
    public Task<Guid> CreatePinAsync(Pin pin);
    public Task<Pin?> GetPinAsync(Guid sessionId, Guid pinId);
    public Task<bool> UpdatePinAsync(Pin pin);
    public Task<bool> DeletePinAsync(Guid sessionId, Guid pinId);

    public Task<IEnumerable<Pin>> GetPinsAsync(Guid sessionId, PinType? type = null);

    public Task<IEnumerable<PinMatch>> KnnSearchPinsAsync(
        Guid sessionId,
        string query,
        int limit,
        PinType? type = null,
        double maxDistance = 2.0);
}
