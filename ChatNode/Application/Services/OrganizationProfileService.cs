using ChatNode.Application.DTO;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using Domain.Repositories;

namespace ChatNode.Application.Services;

public class OrganizationProfileService(IOrganizationProfileRepository repository) : IOrganizationProfileService
{
    public async Task<OrganizationProfileDto> GetAsync(Guid ownerId, CancellationToken ct = default)
    {
        var profile = await repository.GetAsync(ownerId, ct);

        return profile is null ? OrganizationProfileDto.Empty : profile.MapToDto();
    }

    public async Task<OrganizationProfileDto> SaveAsync(
        Guid ownerId,
        OrganizationProfileDto dto,
        CancellationToken ct = default)
    {
        var profile = dto.Trimmed().MapToEntity(ownerId);

        await repository.SaveAsync(profile, ct);

        return profile.MapToDto();
    }
}
