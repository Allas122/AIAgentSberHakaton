using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class UsageService(ITokenUsageRepository repository) : IUsageService
{
    public const int DefaultDays = 30;
    public const int MaxDays = 365;
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    public async Task<UsageRecordPageDto> RecordsAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        TokenOperation? operation,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var (start, end) = Period(role, from, to);

        var take = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var skip = Math.Max(offset, 0);

        var page = await repository.ListAsync(start, end, operation, take, skip, ct);

        var items = page.Items
            .Select(record => new UsageRecordDto(
                record.StartedAt,
                record.Operation,
                record.Model,
                record.PromptTokens,
                record.CompletionTokens,
                record.TotalTokens,
                record.ToolCalls,
                record.Partial,
                record.Failed,
                record.Duration,
                record.BalanceSpent,
                record.ChatId))
            .ToList();

        return new UsageRecordPageDto(items, page.Total, take, skip);
    }

    public async Task<int> ClearAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        if (role != UserRole.Rector)
        {
            throw new PermissionDenied("Чистить журнал расхода может только проректор.");
        }

        var (start, end) = Period(role, from, to);

        return await repository.ClearAsync(start, end, ct);
    }

    private (DateTimeOffset Start, DateTimeOffset End) Period(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!StaffRoles.Contains(role))
        {
            throw new PermissionDenied("Расход токенов виден только проректору и координатору.");
        }

        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-DefaultDays);

        if (start >= end)
        {
            throw new InvalidRequestException("Начало периода должно быть раньше конца.");
        }

        if ((end - start).TotalDays > MaxDays)
        {
            throw new InvalidRequestException($"Период не может быть длиннее {MaxDays} дней.");
        }

        return (start, end);
    }

    public async Task<UsageReportDto> ReportAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var (start, end) = Period(role, from, to);

        var summaries = await repository.SummarizeAsync(start, end, ct);

        var rows = summaries
            .Select(summary => new UsageRowDto(
                summary.Operation,
                summary.Model,
                summary.Calls,
                summary.PartialCalls,
                summary.FailedCalls,
                summary.PromptTokens,
                summary.CompletionTokens,
                summary.TotalTokens,
                summary.Duration,
                summary.Slowest,
                summary.BalanceSpent))
            .ToList();

        return new UsageReportDto(
            start,
            end,
            rows,
            rows.Sum(row => row.TotalTokens),
            TimeSpan.FromTicks(rows.Sum(row => row.Duration.Ticks)),
            rows.Sum(row => row.BalanceSpent),
            rows.Any(row => row.PartialCalls > 0));
    }
}
