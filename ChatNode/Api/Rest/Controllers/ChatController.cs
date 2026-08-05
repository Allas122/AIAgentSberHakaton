
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Chat;
using ChatNode.Api.WebSockets.Hubs;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(
    IChatService chatService,
    IHubContext<ChatHub> chatHub
    ) : ControllerBase
{
    [HttpPost]
    public async Task<CreateChatResponse> CreateChat([FromBody] CreateChatRequest request)
    {
        var userId = HttpContext.User.GetUserId();
        var chatId = await chatService.CreateChatAsync(request.title, userId);
        return new CreateChatResponse(chatId);
    }

    [HttpGet]
    public async Task<List<Chat>> GetChatList(Guid? lastChatId,int limit = 10)
    {
        var userId = HttpContext.User.GetUserId();
        return (await chatService.GetUserChatsAsync(userId,limit, lastChatId)).Select(c=>c.MapToChat()).ToList();
    }
    
    [HttpPost("{chatId}/files")]
    [Consumes("multipart/form-data")]
    public async Task<UploadFileResponse> UploadFileInChat(
        Guid chatId,
        [FromForm] UploadFileRequest request,
        CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var userGroup = chatHub.Clients.Group($"user:{userId}");

        await userGroup.SendAsync("FileStatusChanged",
            new { FileName = request.File.FileName, Status = "Uploaded" }, ct);

        await using var fileStream = request.File.OpenReadStream();

        var review = await chatService.UploadGrantApplicationAsync(
            chatId,
            userId,
            request.ManualId,
            fileStream,
            request.File.FileName,
            request.Content,
            status => userGroup.SendAsync("AiStatusUpdate", status, ct),
            ct);

        await userGroup.SendAsync("FileStatusChanged",
            new { FileName = request.File.FileName, Status = "Reviewed" }, ct);

        return new UploadFileResponse(review.Id, review.Content);
    }
}