using Domain.Entities;
using Domain.ValueTypes;

namespace Domain.Repositories;

public interface IChatRepository
{
    Task<Guid> CreateChatAsync(string title, Guid userId, ChatKind kind = ChatKind.Grant);
    Task<Chat?> GetChatAsync(Guid id);
    Task<IEnumerable<Chat>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId, ChatKind kind);
    Task<bool> UpdateChatAsync(Chat chat);
    Task<bool> DeleteChatAsync(Guid id);
    Task<string> AddMessageAsync(Guid chatId, Message message);
    Task<IEnumerable<Message>> GetMessagesAsync(Guid chatId, int limit, string? lastMessageId = null);
    Task<Message?> GetMessageAsync(Guid chatId, string messageId);
}