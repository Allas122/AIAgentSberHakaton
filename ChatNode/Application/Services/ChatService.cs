using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class ChatService(
    IChatRepository chatRepository,
    IUserRepository userRepository,
    IManualRepository manualRepository,
    AgentFactory agentFactory,
    IDocxAnonymizer docxAnonymizer,
    IDocxTextExtractor docxTextExtractor,
    IAnonymizeClient anonymizeClient,
    IFileStorage fileStorage
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

        ConsultingAgent agent = agentFactory.CreateConsultingAgent(statusHandler, new AgentSession(manualId, chatId));

        var aiResponseText = await agent.InvokeAsync(dto.Content, history, ct);
        aiResponseText = await anonymizeClient.DeanonymizeAsync(aiResponseText, chatId.ToString());

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
    

    public async Task<MessageDisplayDto> UploadGrantApplicationAsync(
        Guid chatId,
        Guid userId,
        Guid manualId,
        Stream fileStream,
        string fileName,
        string? content,
        Action<string> statusHandler,
        CancellationToken ct)
    {
        await TryCheck(chatId, userId);

        if (await manualRepository.GetManualAsync(manualId) is null)
        {
            throw new NotFoundException("Методичка не найдена — проверять заявку не по чему.");
        }

        var sessionId = chatId.ToString();

        statusHandler("Убираю персональные данные из документа...");
        var anonymizedDocx = await docxAnonymizer.AnonymizeAsync(fileStream, sessionId, ct);

        using var anonymizedStream = new MemoryStream(anonymizedDocx, writable: false);
        var sections = docxTextExtractor.ExtractSections(anonymizedStream);

        if (sections.Count == 0)
        {
            throw new InvalidDocumentException("Не удалось извлечь текст из документа — он пустой или состоит из картинок.");
        }

        var applicationId = Guid.NewGuid();
        await fileStorage.UploadAsync(StorageKeys.GrantApplication(chatId, applicationId), anonymizedDocx, ct);

        var userMessageContent = string.IsNullOrWhiteSpace(content)
            ? $"Загружена заявка на проверку: {fileName}"
            : $"Загружена заявка на проверку: {fileName}\n\n{content}";

        await chatRepository.AddMessageAsync(chatId, new MessageDto(null, userMessageContent, userId, DateTime.Now, applicationId).MapToMassage());

        var agent = agentFactory.CreateApplicationReviewAgent(statusHandler, new AgentSession(manualId, chatId));
        var verdict = await agent.ReviewAsync(sections, content, ct);

        verdict = await anonymizeClient.DeanonymizeAsync(verdict, sessionId);

        var agentMessage = new MessageDto(null, verdict, Guid.Empty, DateTime.Now, null);
        var agentRedisId = await chatRepository.AddMessageAsync(chatId, agentMessage.MapToMassage());

        return new MessageDisplayDto(
            Id: agentRedisId,
            Content: verdict,
            UserRole: UserRole.Agent,
            userName: "ApplicationReviewAgent"
        );
    }


}