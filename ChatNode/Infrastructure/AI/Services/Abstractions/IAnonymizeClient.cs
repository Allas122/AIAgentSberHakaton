namespace ChatNode.Infrastructure.AI.Services.Abstractions;

public interface IAnonymizeClient
{
    Task<string> AnonymizeAsync(string text, string sessionId);
    Task<string> DeanonymizeAsync(string text, string sessionId);
}