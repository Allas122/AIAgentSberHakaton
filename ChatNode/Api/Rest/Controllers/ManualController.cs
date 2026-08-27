
using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/manuals")]
public class ManualController(
    IManualService manualService
    ) : ControllerBase
{
    [HttpPost("upload")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadManual([FromForm] UploadManualRequest request, CancellationToken cancellationToken)
    {
        var ownerId = HttpContext.User.GetUserId();

        var queued = await manualService.QueueManualAsync(
            request.Title,
            ownerId,
            request.File.OpenReadStream(),
            request.File.FileName,
            request.Scope ?? ManualScope.Staff,
            cancellationToken);

        return Accepted(queued.MapToUploadManualResponse());
    }

    [HttpGet()]
    public async Task<IEnumerable<ManualData>> GetManuals(CancellationToken cancellationToken)
    {
        var manuals = await manualService.GetManuals(IsStaff);
        return manuals.Select(m=> m.MapToManualData());
    }

    [HttpGet("{manualId:guid}")]
    public async Task<IActionResult> GetManual(Guid manualId, CancellationToken cancellationToken)
    {
        var manual = await manualService.GetManual(manualId, IsStaff);

        return manual is null ? NotFound() : Ok(manual.MapToManualData());
    }

    private bool IsStaff => HttpContext.User.GetUserRole() is UserRole.Rector or UserRole.Coordinator;
}
