using ChatNode.Application.DTO;
using Domain.Entities;

namespace ChatNode.Application.Services.Abstractons;

public interface IChatService
{
    public Task<Guid> CreateChatAsync(string title, Guid userId);
    public Task<List<MessageDisplayDto>> GetMessagesAsync(Guid chatId, Guid userId, int limit, string? lastMessageId);
    public Task<MessageDisplayDto> SendMessageToConsultingAgentAsync(
        Guid chatId,
        Guid userId,
        MessageDto dto,
        Action<string> statusHandler,
        Guid manualId,
        CancellationToken ct
    );
    public Task<List<ChatDto>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId);

    public Task<MessageDisplayDto> UploadGrantApplicationAsync(
        Guid chatId,
        Guid userId,
        Guid manualId,
        Stream fileStream,
        string fileName,
        string? content,
        Action<string> statusHandler,
        CancellationToken ct
    );
}