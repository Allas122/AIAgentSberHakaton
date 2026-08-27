using ChatNode.Application.DTO;

namespace ChatNode.Application.Services.Abstractons;

public interface IOrganizationProfileService
{
    Task<OrganizationProfileDto> GetAsync(Guid ownerId, CancellationToken ct = default);

    Task<OrganizationProfileDto> SaveAsync(Guid ownerId, OrganizationProfileDto dto, CancellationToken ct = default);
}
