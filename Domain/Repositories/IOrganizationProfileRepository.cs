using Domain.Entities;

namespace Domain.Repositories;

public interface IOrganizationProfileRepository
{
    Task<OrganizationProfile?> GetAsync(Guid ownerId, CancellationToken ct = default);

    Task SaveAsync(OrganizationProfile profile, CancellationToken ct = default);
}
