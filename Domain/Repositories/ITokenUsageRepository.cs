using Domain.Entities;
using Domain.ValueTypes;

namespace Domain.Repositories;

public record TokenUsageSummary(
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

public record TokenUsagePage(IReadOnlyList<TokenUsage> Items, int Total);

public interface ITokenUsageRepository
{
    Task AddAsync(TokenUsage usage, CancellationToken ct = default);

    Task<TokenUsagePage> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        TokenOperation? operation,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<IReadOnlyList<TokenUsageSummary>> SummarizeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    Task<int> ClearAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
