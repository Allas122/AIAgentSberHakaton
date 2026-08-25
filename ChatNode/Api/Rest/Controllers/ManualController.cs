
using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
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
            cancellationToken);

        return Accepted(queued.MapToUploadManualResponse());
    }

    [HttpGet()]
    public async Task<IEnumerable<ManualData>> GetManuals(CancellationToken cancellationToken)
    {
        var manuals = await manualService.GetManuals();
        return manuals.Select(m=> m.MapToManualData());
    }

    [HttpGet("{manualId:guid}")]
    public async Task<IActionResult> GetManual(Guid manualId, CancellationToken cancellationToken)
    {
        var manual = await manualService.GetManual(manualId);

        return manual is null ? NotFound() : Ok(manual.MapToManualData());
    }
}
