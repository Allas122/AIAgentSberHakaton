namespace ChatNode.Infrastructure.Auth.Abstractions;

public interface ITicketProvider
{
    public Task<Guid> GetTicketAsync(Guid userId);
    public Task<Guid> GetUserIdByTicketAsync(Guid ticketId);
}