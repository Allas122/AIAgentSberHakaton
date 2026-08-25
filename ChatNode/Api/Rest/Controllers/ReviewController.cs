using ChatNode.Api.Rest.Messages.Reviews;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reviews")]
public class ReviewController(IChatService chatService) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IReadOnlyList<ActiveReviewResponse>> Active()
    {
        var running = await chatService.GetActiveReviewsAsync(HttpContext.User.GetUserId());

        return running
            .Select(r => new ActiveReviewResponse(r.DocumentId, r.ChatId, r.FileName, r.Stage, r.Detail))
            .ToList();
    }
}
