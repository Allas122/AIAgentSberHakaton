using ChatNode.Infrastructure.Auth.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatNode.Infrastructure.Auth;

public class TicketProvider(
    IDatabase database,
    IOptions<WebSocketOption> options
    ) : ITicketProvider
{
    private WebSocketOption _webSocketOptions = options.Value;
    private const string TicketKeyPrefix = "ws_ticket:";

    public async Task<Guid> GetTicketAsync(Guid userId)
    {
        var ticketId = Guid.NewGuid();
        var key = $"{TicketKeyPrefix}{ticketId}";
        await database.StringSetAsync(key, userId.ToString(), TimeSpan.FromSeconds(_webSocketOptions.TicketExpirationInSeconds));
        return ticketId;
    }

    public async Task<Guid> GetUserIdByTicketAsync(Guid ticketId)
    {
        var key = $"{TicketKeyPrefix}{ticketId}";
        var userIdRaw = await database.StringGetAsync(key);

        if (userIdRaw.IsNull)
        {
            return Guid.Empty;
        }

        return Guid.Parse(userIdRaw.ToString()!);
    }
}
