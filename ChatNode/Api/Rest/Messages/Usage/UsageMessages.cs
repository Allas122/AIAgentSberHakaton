namespace ChatNode.Api.Rest.Messages.Usage;

public record UsageRowResponse(
    string Operation,
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

public record UsageRecordResponse(
    DateTimeOffset StartedAt,
    string Operation,
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

public record UsageRecordPageResponse(
    IReadOnlyList<UsageRecordResponse> Items,
    int Total,
    int Limit,
    int Offset);

public record UsageReportResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<UsageRowResponse> Rows,
    long TotalTokens,
    TimeSpan TotalDuration,
    double BalanceSpent,
    bool HasPartial);

public record UsageClearedResponse(int Removed);
