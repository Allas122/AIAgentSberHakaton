using ChatNode.Infrastructure.Database;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class TokenUsageRepository(AppDbContext context) : ITokenUsageRepository
{
    public async Task AddAsync(TokenUsage usage, CancellationToken ct = default)
    {
        usage.Id = usage.Id == Guid.Empty ? Guid.NewGuid() : usage.Id;

        context.TokenUsages.Add(usage);
        await context.SaveChangesAsync(ct);
    }

    public async Task<TokenUsagePage> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        TokenOperation? operation,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var query = context.TokenUsages
            .AsNoTracking()
            .Where(x => x.StartedAt >= from && x.StartedAt < to);

        if (operation is not null) query = query.Where(x => x.Operation == operation.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Skip(Math.Max(offset, 0))
            .Take(limit)
            .ToListAsync(ct);

        return new TokenUsagePage(items, total);
    }

    public async Task<int> ClearAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await context.TokenUsages
            .Where(x => x.StartedAt >= from && x.StartedAt < to)
            .ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<TokenUsageSummary>> SummarizeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var rows = await context.TokenUsageAggregates
            .FromSql(
                $"""
                 SELECT
                     "Operation"                                              AS "Operation",
                     "Model"                                                  AS "Model",
                     count(*)                                                 AS "Calls",
                     count(*) FILTER (WHERE "Partial")                        AS "PartialCalls",
                     count(*) FILTER (WHERE "Failed")                         AS "FailedCalls",
                     coalesce(sum("PromptTokens"), 0)                         AS "PromptTokens",
                     coalesce(sum("CompletionTokens"), 0)                     AS "CompletionTokens",
                     coalesce(sum("TotalTokens"), 0)                          AS "TotalTokens",
                     coalesce(sum(extract(epoch from "Duration")), 0)::float8 AS "DurationSeconds",
                     coalesce(max(extract(epoch from "Duration")), 0)::float8 AS "SlowestSeconds",
                     coalesce(sum("BalanceSpent"), 0)::float8                 AS "BalanceSpent"
                 FROM token_usage
                 WHERE "StartedAt" >= {from} AND "StartedAt" < {to}
                 GROUP BY "Operation", "Model"
                 """)
            .ToListAsync(ct);

        return rows
            .Select(row => new TokenUsageSummary(
                (TokenOperation)row.Operation,
                row.Model,
                (int)row.Calls,
                (int)row.PartialCalls,
                (int)row.FailedCalls,
                row.PromptTokens,
                row.CompletionTokens,
                row.TotalTokens,
                TimeSpan.FromSeconds(row.DurationSeconds),
                TimeSpan.FromSeconds(row.SlowestSeconds),
                row.BalanceSpent))
            .OrderByDescending(summary => summary.TotalTokens)
            .ThenByDescending(summary => summary.Duration)
            .ToList();
    }
}
