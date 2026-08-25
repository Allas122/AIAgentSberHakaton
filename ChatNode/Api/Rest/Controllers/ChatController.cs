
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Chat;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Review;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<CreateChatResponse> CreateChat([FromBody] CreateChatRequest request)
    {
        var userId = HttpContext.User.GetUserId();
        var chatId = await chatService.CreateChatAsync(request.title, userId, request.kind);
        return new CreateChatResponse(chatId);
    }

    [HttpGet]
    public async Task<List<Chat>> GetChatList(
        Guid? lastChatId,
        int limit = 10,
        [FromQuery] ChatKind kind = ChatKind.Grant)
    {
        var userId = HttpContext.User.GetUserId();
        return (await chatService.GetUserChatsAsync(userId, limit, lastChatId, kind)).Select(c => c.MapToChat()).ToList();
    }
    
    [HttpPost("{chatId:guid}/files")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadFileResponse>> UploadFileInChat(
        Guid chatId,
        [FromForm] UploadFileRequest request,
        CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();

        await using var fileStream = request.File.OpenReadStream();

        var queued = await chatService.UploadGrantApplicationAsync(
            chatId,
            userId,
            request.ManualId,
            fileStream,
            request.File.FileName,
            request.Content,
            request.ContestKind,
            ct);

        return Accepted(new UploadFileResponse(
            queued.MessageId,
            queued.ApplicationId,
            queued.DocumentId,
            queued.FileName,
            queued.QueueDepth));
    }

    [HttpDelete("{chatId:guid}")]
    public async Task<IActionResult> DeleteChat(Guid chatId, CancellationToken ct)
    {
        await chatService.DeleteChatAsync(chatId, HttpContext.User.GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("{chatId:guid}/messages/{messageId}/review.docx")]
    public async Task<IActionResult> ExportReview(Guid chatId, string messageId)
    {
        var userId = HttpContext.User.GetUserId();
        var file = await chatService.ExportReviewAsync(chatId, userId, messageId);

        return File(
            file.Content,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            file.FileName);
    }
}