using ChatNode.Api.WebSockets.Mappers;
using ChatNode.Api.WebSockets.Messages;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.WebSockets.Hubs;

[Authorize(AuthenticationSchemes = "WebSocketScheme")]
public class ChatHub(IChatService chatService) : Hub
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
        => await chatService.CreateChatAsync(request.title, CurrentUserId);

    public async Task<List<Chat>> GetChatList(Guid? lastChatId, int limit = 10)
        => (await chatService.GetUserChatsAsync(CurrentUserId, limit, lastChatId))
            .Select(c => c.MapToChat()).ToList();

    public async Task<List<MessageReturn>> GetMessages(Guid chatId, string? lastMessageId, int limit = 10)
    {
        var messages = await chatService.GetMessagesAsync(chatId, CurrentUserId, limit, lastMessageId);
        return messages.Select(m => new MessageReturn(m.Id, m.userName, m.UserRole, m.Content)).ToList();
    }

    public async Task<MessageReturn> SendMessageToConsultingAgent(Guid chatId, string content, Guid manualId)
    {
        var userId = CurrentUserId;
        var userDto = new MessageDto(null, content, userId, DateTime.UtcNow, null);

        var response = await chatService.SendMessageToConsultingAgentAsync(
            chatId,
            userId,
            userDto,
            status => Clients.Group($"user:{userId}").SendAsync("AiStatusUpdate", status),
            manualId,
            Context.ConnectionAborted
        );

        return new MessageReturn(response.Id, response.userName, response.UserRole, response.Content);
    }
}