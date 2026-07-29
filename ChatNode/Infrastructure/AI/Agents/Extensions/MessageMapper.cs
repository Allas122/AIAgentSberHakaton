using ChatNode.Infrastructure.Dto;
using Domain.ValueTypes;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Agents.Extensions;

public static class MessageMapper
{
    public static Messages? ToMessages(this MessageHistoricalDto message)
    {
        return message.SenderRole switch
        {
            UserRole.User => Messages.User(message.Content),
            UserRole.Agent => Messages.Assistant(message.Content)
        };
    }
}