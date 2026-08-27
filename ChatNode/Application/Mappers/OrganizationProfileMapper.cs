using ChatNode.Application.DTO;
using Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace ChatNode.Application.Mappers;

[Mapper]
public static partial class OrganizationProfileMapper
{
    public static OrganizationProfileDto MapToDto(this OrganizationProfile profile) =>
        profile.MapToDtoCore() with
        {
            UpdatedAt = profile.UpdatedAt == default ? null : profile.UpdatedAt
        };

    public static OrganizationProfile MapToEntity(this OrganizationProfileDto dto, Guid ownerId)
    {
        var profile = dto.MapToEntityCore();
        profile.OwnerId = ownerId;

        return profile;
    }

    [MapperIgnoreSource(nameof(OrganizationProfile.OwnerId))]
    private static partial OrganizationProfileDto MapToDtoCore(this OrganizationProfile profile);

    [MapperIgnoreSource(nameof(OrganizationProfileDto.UpdatedAt))]
    [MapperIgnoreSource(nameof(OrganizationProfileDto.HasSigner))]
    [MapperIgnoreSource(nameof(OrganizationProfileDto.HasContact))]
    [MapperIgnoreSource(nameof(OrganizationProfileDto.IsEmpty))]
    [MapperIgnoreTarget(nameof(OrganizationProfile.OwnerId))]
    [MapperIgnoreTarget(nameof(OrganizationProfile.UpdatedAt))]
    private static partial OrganizationProfile MapToEntityCore(this OrganizationProfileDto dto);
}
