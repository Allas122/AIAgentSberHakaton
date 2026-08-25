using ChatNode.Infrastructure.Review;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.WebSockets.Hubs;

public class HubReviewNotifier(IHubContext<ChatHub> chatHub) : IReviewNotifier
{
    public Task StatusAsync(Guid userId, Guid chatId, string status)
        => Group(userId).SendAsync("AiStatusUpdate", new { ChatId = chatId, Status = status });

    public Task FileStatusAsync(Guid userId, string fileName, string status)
        => Group(userId).SendAsync("FileStatusChanged", new { FileName = fileName, Status = status });

    public Task ReviewReadyAsync(Guid userId, Guid chatId, Guid? documentId, string messageId, string content)
        => Group(userId).SendAsync("ReviewReady",
            new { ChatId = chatId, DocumentId = documentId, MessageId = messageId, Content = content });

    public Task ReviewFailedAsync(Guid userId, Guid chatId, string fileName, string messageId, string reason)
        => Group(userId).SendAsync("ReviewFailed",
            new { ChatId = chatId, FileName = fileName, MessageId = messageId, Reason = reason });

    private IClientProxy Group(Guid userId) => chatHub.Clients.Group($"user:{userId}");
}
