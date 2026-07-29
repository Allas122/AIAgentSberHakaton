
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Chat;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(
    IChatService chatService
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

    // [HttpPost("{chatId}/files")]
    // [RequestSizeLimit(52428800)]
    // [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] 
    // public async Task<Guid> UploadFileInChat(UploadFileRequest request)
    // {
    //     
    // }
}