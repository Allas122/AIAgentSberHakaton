using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record UsageRowDto(
    TokenOperation Operation,
    string Model,
    int Calls,
    int PartialCalls,
    int FailedCalls,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    TimeSpan Duration,
    TimeSpan Slowest,
    double BalanceSpent);

public record UsageRecordDto(
    DateTimeOffset StartedAt,
    TokenOperation Operation,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    int ToolCalls,
    bool Partial,
    bool Failed,
    TimeSpan Duration,
    double? BalanceSpent,
    Guid? ChatId);

public record UsageRecordPageDto(
    IReadOnlyList<UsageRecordDto> Items,
    int Total,
    int Limit,
    int Offset);

public record UsageReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<UsageRowDto> Rows,
    long TotalTokens,
    TimeSpan TotalDuration,
    double BalanceSpent,
    bool HasPartial);
