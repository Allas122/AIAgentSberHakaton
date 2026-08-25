using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Application.Exceptions;
using ChatNode.Infrastructure.Mappers;
using ChatNode.Infrastructure.Tools.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure;

public class ChatRepository(
    IDatabase database,
    IOptions<ExpirationPolicyOption> exOptions,
    IExpirationSustainerTool expirationSustainerTool) : IChatRepository
{
    private const int MaxChatScan = 500;

    private readonly ExpirationPolicyOption _exOptions = exOptions.Value;

    public async Task<Guid> CreateChatAsync(string title, Guid userId, ChatKind kind = ChatKind.Grant)
    {
        var id = Guid.NewGuid();
        var chatKey = $"chat:{id}";
        var userChatsKey = $"user:chats:{userId}";
        var messageStreamKey = $"chat:message-stream:{id}";

        var chat = new Chat(id, userId, title, kind);
        
        var expirationTimestamp = DateTimeOffset.UtcNow.AddSeconds(_exOptions.ChatExpirationSeconds).ToUnixTimeSeconds();

        var transaction = database.CreateTransaction();
        _ = transaction.HashSetAsync(chatKey, chat.ToHashEntries());
        
        _ = transaction.SortedSetAddAsync(userChatsKey, id.ToString(), expirationTimestamp);
        
        _ = transaction.KeyExpireAsync(chatKey, TimeSpan.FromSeconds(_exOptions.ChatExpirationSeconds));
        _ = transaction.KeyExpireAsync(userChatsKey, TimeSpan.FromSeconds(_exOptions.UserExpirationSeconds));
        _ = transaction.KeyExpireAsync(messageStreamKey, TimeSpan.FromSeconds(_exOptions.MessageStreamExpirationSeconds));

        var committed = await transaction.ExecuteAsync();
        if (!committed) throw new Exception("Failed to create chat in Valkey.");

        return id;
    }

    public async Task<Chat?> GetChatAsync(Guid id)
    {
        var chatKey = $"chat:{id}";
        var fields = await database.HashGetAllAsync(chatKey);
        
        if (fields.Length == 0) return null;
        
        await expirationSustainerTool.SustainByChatIdAsync(id);
        
        return fields.ToChat(id);
    }

    public async Task<IEnumerable<Chat>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId, ChatKind kind)
    {
        var userChatsKey = $"user:chats:{userId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await database.SortedSetRemoveRangeByScoreAsync(userChatsKey, 0, now);

        double maxScore = double.PositiveInfinity;

        if (lastChatId.HasValue && lastChatId != Guid.Empty)
        {
            var score = await database.SortedSetScoreAsync(userChatsKey, lastChatId.Value.ToString());
            if (score.HasValue)
            {
                maxScore = score.Value - 0.0001;
            }
        }

        var chats = new List<Chat>();
        var scanned = 0;

        while (chats.Count < limit && scanned < MaxChatScan)
        {
            var page = await database.SortedSetRangeByScoreWithScoresAsync(
                userChatsKey,
                stop: maxScore,
                start: 0,
                order: Order.Descending,
                take: limit);

            if (page.Length == 0) break;

            scanned += page.Length;

            foreach (var entry in page)
            {
                var fields = await database.HashGetAllAsync($"chat:{entry.Element}");
                if (fields.Length == 0) continue;

                var chat = fields.ToChat(Guid.Parse(entry.Element.ToString()));
                if (chat.Kind != kind) continue;

                chats.Add(chat);
                if (chats.Count == limit) break;
            }

            maxScore = page[^1].Score - 0.0001;
        }

        return chats;
    }

    public async Task<bool> UpdateChatAsync(Chat chat)
    {
        var chatKey = $"chat:{chat.Id}";
        if (!await database.KeyExistsAsync(chatKey)) return false;

        await database.HashSetAsync(chatKey, chat.ToHashEntries());
        await expirationSustainerTool.SustainByChatIdAsync(chat.Id);
        return true;
    }

    public async Task<bool> DeleteChatAsync(Guid id)
    {
        var chatKey = $"chat:{id}";
        var messageStreamKey = $"chat:message-stream:{id}";
        var userIdRaw = await database.HashGetAsync(chatKey, "UserId");

        var transaction = database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(chatKey);
        _ = transaction.KeyDeleteAsync(messageStreamKey);

        if (!userIdRaw.IsNull)
        {
            var userChatsKey = $"user:chats:{userIdRaw}";
            _ = transaction.SortedSetRemoveAsync(userChatsKey, id.ToString());
        }

        return await transaction.ExecuteAsync();
    }

    public async Task<string> AddMessageAsync(Guid chatId, Message message)
    { 
        if (!await database.KeyExistsAsync($"chat:{chatId}")) 
            throw new NotFoundException("Чат не найден — возможно, он истёк.");

        var messageStreamKey = $"chat:message-stream:{chatId}";
        var messageToSave = message with { CreateAt = DateTime.Now };
        var redisId = await database.StreamAddAsync(messageStreamKey, messageToSave.ToStreamEntries());
        await expirationSustainerTool.SustainByChatIdAsync(chatId);
        
        return redisId.ToString()!;
    }

    public async Task<IEnumerable<Message>> GetMessagesAsync(Guid chatId, int limit, string? lastMessageId)
    {
        var messageStreamKey = $"chat:message-stream:{chatId}";

        string startId = string.IsNullOrEmpty(lastMessageId) ? "+" : "(" + lastMessageId;

        var result = await database.ExecuteAsync(
            "XREVRANGE", 
            messageStreamKey, 
            startId, 
            "-", 
            "COUNT", 
            limit);

        RedisResult[]? entries = (RedisResult[]?)result;
        if (entries == null || entries.Length == 0)
            return Enumerable.Empty<Message>();
        return entries
            .Select(e => e.ToMessage())
            .Reverse()
            .ToList();
    }

    public async Task<Message?> GetMessageAsync(Guid chatId, string messageId)
    {
        var messageStreamKey = $"chat:message-stream:{chatId}";

        var result = await database.ExecuteAsync("XRANGE", messageStreamKey, messageId, messageId, "COUNT", 1);

        var entries = (RedisResult[]?)result;
        if (entries is null || entries.Length == 0) return null;

        await expirationSustainerTool.SustainByChatIdAsync(chatId);

        return entries[0].ToMessage();
    }
}