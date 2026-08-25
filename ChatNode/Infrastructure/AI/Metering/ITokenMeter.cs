using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Metering;

public record TokenRecord(
    TokenOperation Operation,
    string Model,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    int TotalTokens = 0,
    int ToolCalls = 0,
    bool Partial = false,
    bool Failed = false,
    double? BalanceSpent = null,
    Guid? ChatId = null,
    Guid? UserId = null);

public interface ITokenMeter
{
    Task RecordAsync(TokenRecord record, CancellationToken ct = default);
}
