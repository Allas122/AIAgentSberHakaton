using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Dto;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class ChatService(
    IChatRepository chatRepository,
    IUserRepository userRepository,
    AgentFactory agentFactory
    ) : IChatService
{
    private async Task TryCheck(Guid chatId, Guid userId)
    {
        var chat = await chatRepository.GetChatAsync(chatId);
        if (chat is null) throw new NotFoundException("Chat not found");
        if (chat.UserId != userId) throw new PermissionDenied("Permission denied");
    }
    
    public async Task<Guid> CreateChatAsync(string title,  Guid userId)
    {
        var chatId = await chatRepository.CreateChatAsync(title, userId);
        return chatId;
    }

    public async Task<List<MessageDisplayDto>> GetMessagesAsync(Guid chatId,Guid userId, int limit, string? lastMessageId)
    {
        await  TryCheck(chatId, userId);
        var messages = await chatRepository.GetMessagesAsync(chatId, limit, lastMessageId);
        List<MessageDisplayDto> result=[];
        foreach (var m in messages)
        {
            var senderRole = UserRole.Agent;
            var senderName = "Agent";
            if (m.SenderId != Guid.Empty)
            {
                var user = await userRepository.GetUserAsync(m.SenderId);
                senderRole = user.Role;
                senderName = user.Name;
            }
            MessageDisplayDto messageDispay = new(m.Id, m.Content, senderRole, senderName);
        }
        return result;
    }

    public async Task<MessageDisplayDto> SendMessageToConsultingAgentAsync(
        Guid chatId,
        Guid userId,
        MessageDto dto,
        Action<string> statusHandler,
        Guid manualId,
        CancellationToken ct =  default(CancellationToken)
        )
    {
        await TryCheck(chatId, userId);
        await chatRepository.AddMessageAsync(chatId, dto.MapToMassage());

        var messages = await chatRepository.GetMessagesAsync(chatId, limit: 15);
        var history = messages.Select(m => new MessageHistoricalDto
        {
            Content = m.Content,
            SenderRole = m.SenderId == Guid.Empty ? UserRole.Agent : UserRole.User
        });

        ConsultingAgent agent = agentFactory.CreateConsultingAgent(statusHandler, manualId);
    
        var aiResponseText = await agent.InvokeAsync(dto.Content, history, ct);

        var aiMessage = new MessageDto(null, aiResponseText, Guid.Empty, DateTime.Now, null);
        var aiRedisId = await chatRepository.AddMessageAsync(chatId, aiMessage.MapToMassage());

        return new MessageDisplayDto(
            Id: aiRedisId, 
            Content: aiResponseText, 
            UserRole: UserRole.Agent, 
            userName: "ConsultingAgent" 
        );
    }

    public async Task<List<ChatDto>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId)
    {
        return (await chatRepository.GetUserChatsAsync(userId, limit, lastChatId)).Select(c=>c.MapToChatDto()).ToList();
    }
}