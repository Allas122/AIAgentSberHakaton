using ChatNode.Api.WebSockets.Mappers;
using ChatNode.Api.WebSockets.Messages;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatNode.Api.WebSocket.Messages.Messages;

[Authorize(AuthenticationSchemes = "WebSocketScheme")]
public class ChatHub(IChatService chatService) : Hub
{
    public async Task<Guid> CreateChat(CreateChat request)
    {
        Guid.TryParse(Context.UserIdentifier, out var userId);
        var chatId = await chatService.CreateChatAsync(request.title, userId);
        return chatId;
    }
    
    public async Task<List<Chat>> GetChatList(Guid? lastChatId,int limit = 10)
    {
        Guid.TryParse(Context.UserIdentifier, out var userId);
        return (await chatService.GetUserChatsAsync(userId,limit, lastChatId)).Select(c=>c.MapToChat()).ToList();
    }
    public async Task<List<MessageReturn>> GetMessages(Guid chatId ,string? lastMessageId,int limit = 10)
    {
        Guid.TryParse(Context.UserIdentifier, out var userId);
        if (userId == Guid.Empty)
        {
            throw new HubException("Unauthorized");
        }
        var messages = await chatService.GetMessagesAsync(chatId, userId,limit, lastMessageId);
        return messages.Select(m=> new MessageReturn(m.Id, m.userName, m.UserRole,m.Content)).ToList();
    }

    public async Task<MessageReturn> SendMessageToConsultingAgent(Guid chatId, string content, Guid manualId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            throw new HubException("Unauthorized");

        var userDto = new MessageDto(null, content, userId, DateTime.Now, null);

        var response = await chatService.SendMessageToConsultingAgentAsync(
            chatId, 
            userId, 
            userDto, 
            status => Clients.Caller.SendAsync("AiStatusUpdate", status), 
            manualId, 
            Context.ConnectionAborted
        );

        var result = new MessageReturn(response.Id, response.userName, response.UserRole, response.Content);
        return result;
    }
}