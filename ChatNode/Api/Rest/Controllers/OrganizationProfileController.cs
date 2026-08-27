using ChatNode.Api.Rest.Messages.Organization;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Rector))]
[Route("api/organization-profile")]
public class OrganizationProfileController(IOrganizationProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<OrganizationProfileResponse> Get(CancellationToken ct) =>
        Map(await profileService.GetAsync(HttpContext.User.GetUserId(), ct));

    [HttpPut]
    public async Task<OrganizationProfileResponse> Save(
        [FromBody] OrganizationProfileRequest request,
        CancellationToken ct)
    {
        var saved = await profileService.SaveAsync(
            HttpContext.User.GetUserId(),
            new OrganizationProfileDto(
                request.FullName ?? string.Empty,
                request.ShortName ?? string.Empty,
                request.Address ?? string.Empty,
                request.Phone ?? string.Empty,
                request.Fax ?? string.Empty,
                request.Email ?? string.Empty,
                request.Website ?? string.Empty,
                request.Okpo ?? string.Empty,
                request.Ogrn ?? string.Empty,
                request.Inn ?? string.Empty,
                request.Kpp ?? string.Empty,
                request.SignerPosition ?? string.Empty,
                request.SignerName ?? string.Empty,
                request.ContactName ?? string.Empty,
                request.ContactPosition ?? string.Empty,
                request.ContactPhone ?? string.Empty,
                request.ContactEmail ?? string.Empty,
                request.ExecutorName ?? string.Empty,
                request.ExecutorPhone ?? string.Empty,
                UpdatedAt: null),
            ct);

        return Map(saved);
    }

    private static OrganizationProfileResponse Map(OrganizationProfileDto dto) =>
        new(dto.FullName,
            dto.ShortName,
            dto.Address,
            dto.Phone,
            dto.Fax,
            dto.Email,
            dto.Website,
            dto.Okpo,
            dto.Ogrn,
            dto.Inn,
            dto.Kpp,
            dto.SignerPosition,
            dto.SignerName,
            dto.ContactName,
            dto.ContactPosition,
            dto.ContactPhone,
            dto.ContactEmail,
            dto.ExecutorName,
            dto.ExecutorPhone,
            dto.UpdatedAt);
}
