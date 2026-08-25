using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Agents;

public record AgentSession(
    Guid? ManualId,
    Guid ChatId,
    Guid UserId,
    ChatKind Kind = ChatKind.Grant,
    string? TimeZoneId = null)
{
    public TimeZoneInfo ResolveTimeZone(string fallbackId)
    {
        return Find(TimeZoneId) ?? Find(fallbackId) ?? TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
