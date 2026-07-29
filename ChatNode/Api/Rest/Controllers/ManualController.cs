
using ChatNode.Api.Rest.Messages.Manual;
using ChatNode.Infrastructure.AI.Agents;
using Microsoft.AspNetCore.Mvc;


namespace ChatNode.Api.Controllers;

[ApiController]
[Route("api/manuals")]
public class ManualController(
    TestAgent agent,
    ConsultingAgent consultingAgent
    ) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadManual([FromForm] UploadManualRequest request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("File is empty");
        
        using var reader = new StreamReader(request.File.OpenReadStream());
        string content = await reader.ReadToEndAsync();
        
        await agent.InvokeAsync(content,cancellationToken);
        
        return Ok(new { 
            FileName = request.File.FileName, 
            Size = request.File.Length,
            Message = "Sucess" 
        });
    }
    
}