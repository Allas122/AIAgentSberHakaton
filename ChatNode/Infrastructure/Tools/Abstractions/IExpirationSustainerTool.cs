namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IExpirationSustainerTool
{
    public Task SustainByUserIdAsync(Guid userId);
    public Task SustainByChatIdAsync(Guid chatId);
    public Task SustainByMessageStreamIdAsync(Guid messageStreamId);
}