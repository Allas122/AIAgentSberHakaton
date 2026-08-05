
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using Microsoft.AspNetCore.Mvc;


namespace ChatNode.Api.Controllers;

[ApiController]
[Route("api/manuals")]
public class ManualController(
    IManualService manualService
    ) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadManual([FromForm] UploadManualRequest request, CancellationToken cancellationToken)
    {
        return Ok(await manualService.ProcessManual(request.Title,request.File.OpenReadStream(), cancellationToken));
    }

    [HttpGet()]
    public async Task<IEnumerable<ManualData>> GetManuals(CancellationToken cancellationToken)
    {
        var manuals = await manualService.GetManuals();
        return manuals.Select(m=> m.MapToManualData());
    }
}