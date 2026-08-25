using ChatNode.Api.Configuration;
using ChatNode.Api.WebSockets.Mappers;
using ChatNode.Api.WebSockets.Messages;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.WebSockets.Hubs;

[Authorize(AuthenticationSchemes = "WebSocketScheme")]
public class ChatHub(IChatService chatService, IHubContext<ChatHub> hubContext) : Hub
{
    private Guid CurrentUserId
    {
        get
        {
            if (!Guid.TryParse(Context.UserIdentifier, out var userId) || userId == Guid.Empty)
                throw new HubException("Unauthorized");
            return userId;
        }
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{CurrentUserId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<Guid> CreateChat(CreateChat request)
        => await chatService.CreateChatAsync(request.title, CurrentUserId, request.kind);

    public async Task<List<Chat>> GetChatList(Guid? lastChatId, int limit = 10, string? kind = null)
        => (await chatService.GetUserChatsAsync(CurrentUserId, limit, lastChatId, ParseKind(kind)))
            .Select(c => c.MapToChat()).ToList();

    private static ChatKind ParseKind(string? kind) =>
        Enum.TryParse<ChatKind>(kind, ignoreCase: true, out var parsed) ? parsed : ChatKind.Grant;

    public async Task<List<MessageReturn>> GetMessages(Guid chatId, string? lastMessageId, int limit = 10)
    {
        var messages = await chatService.GetMessagesAsync(chatId, CurrentUserId, limit, lastMessageId);
        return messages.Select(ToReturn).ToList();
    }

    public async Task<MessageReturn> SendMessageToConsultingAgent(Guid chatId, string content, Guid? manualId, string? timeZone = null)
    {
        var userId = CurrentUserId;
        var userDto = new MessageDto(string.Empty, content, userId, DateTime.UtcNow);

        var group = hubContext.Clients.Group($"user:{userId}");

        MessageDisplayDto response;

        try
        {
            response = await chatService.SendMessageToConsultingAgentAsync(
                chatId,
                userId,
                userDto,
                status => group.SendAsync("AiStatusUpdate", new { ChatId = chatId, Status = status }),
                manualId,
                timeZone,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            throw new HubException(DomainErrors.Describe(ex).Title);
        }

        var result = ToReturn(response);

        await group.SendAsync("MessageReady", new { ChatId = chatId, Message = result });

        return result;
    }

    private static MessageReturn ToReturn(MessageDisplayDto message) =>
        new(message.Id,
            message.userName,
            message.UserRole,
            message.Content,
            message.File is { } file
                ? new MessageFileReturn(file.DocumentId, file.FileName, file.ReviewMessageId)
                : null);
}