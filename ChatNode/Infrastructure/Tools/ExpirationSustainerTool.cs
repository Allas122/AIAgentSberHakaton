using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Application.Exceptions;
using ChatNode.Infrastructure.Tools.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Tools;

public class ExpirationSustainerTool(
    IDatabase database,
    IOptions<ExpirationPolicyOption> options) : IExpirationSustainerTool
{
    private readonly ExpirationPolicyOption _policy = options.Value;

    public async Task SustainByUserIdAsync(Guid userId)
    {
        var userKey = $"user:{userId}";
        var userChatsKey = $"user:chats:{userId}";

        var batch = database.CreateBatch();

        var pending = new[]
        {
            batch.KeyExpireAsync(userKey, TimeSpan.FromSeconds(_policy.UserExpirationSeconds)),
            batch.KeyExpireAsync(userChatsKey, TimeSpan.FromSeconds(_policy.UserExpirationSeconds))
        };

        batch.Execute();

        await Task.WhenAll(pending);
    }

    public async Task SustainByChatIdAsync(Guid chatId)
    {
        var chatKey = $"chat:{chatId}";
        var streamKey = $"chat:message-stream:{chatId}";

        var userIdRaw = await database.HashGetAsync(chatKey, "UserId");

        if (userIdRaw.IsNull || !Guid.TryParse(userIdRaw.ToString(), out var userId))
            throw new NotFoundException("Владелец чата не найден — возможно, сессия истекла.");

        var userChatsKey = $"user:chats:{userId}";
        
        var newExpirationTime = DateTimeOffset.UtcNow.AddSeconds(_policy.ChatExpirationSeconds);

        var batch = database.CreateBatch();

        var pending = new Task[]
        {
            batch.KeyExpireAsync(chatKey, TimeSpan.FromSeconds(_policy.ChatExpirationSeconds)),
            batch.KeyExpireAsync(streamKey, TimeSpan.FromSeconds(_policy.MessageStreamExpirationSeconds)),
            batch.SortedSetAddAsync(userChatsKey, chatId.ToString(), newExpirationTime.ToUnixTimeSeconds()),
            batch.KeyExpireAsync(userChatsKey, TimeSpan.FromSeconds(_policy.UserExpirationSeconds)),
            batch.KeyExpireAsync($"user:{userId}", TimeSpan.FromSeconds(_policy.UserExpirationSeconds))
        };

        batch.Execute();

        await Task.WhenAll(pending);
    }

    public async Task SustainByMessageStreamIdAsync(Guid messageStreamId)
    {
        await SustainByChatIdAsync(messageStreamId);
    }
}