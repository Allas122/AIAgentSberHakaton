using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Messages.Usage;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
[Route("api/usage")]
public class UsageController(IUsageService usageService) : ControllerBase
{
    [HttpGet]
    public async Task<UsageReportResponse> Report(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var report = await usageService.ReportAsync(HttpContext.User.GetUserRole(), from, to, ct);

        return new UsageReportResponse(
            report.From,
            report.To,
            report.Rows.Select(Map).ToList(),
            report.TotalTokens,
            report.TotalDuration,
            report.BalanceSpent,
            report.HasPartial);
    }

    [HttpGet("records")]
    public async Task<UsageRecordPageResponse> Records(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] TokenOperation? operation,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var page = await usageService.RecordsAsync(
            HttpContext.User.GetUserRole(), from, to, operation, limit, offset, ct);

        return new UsageRecordPageResponse(
            page.Items.Select(Map).ToList(),
            page.Total,
            page.Limit,
            page.Offset);
    }

    [HttpDelete]
    public async Task<UsageClearedResponse> Clear(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var removed = await usageService.ClearAsync(HttpContext.User.GetUserRole(), from, to, ct);

        return new UsageClearedResponse(removed);
    }

    private static UsageRecordResponse Map(UsageRecordDto record) =>
        new(record.StartedAt,
            record.Operation.ToString(),
            record.Model,
            record.PromptTokens,
            record.CompletionTokens,
            record.TotalTokens,
            record.ToolCalls,
            record.Partial,
            record.Failed,
            record.Duration,
            record.BalanceSpent,
            record.ChatId);

    private static UsageRowResponse Map(UsageRowDto row) =>
        new(row.Operation.ToString(),
            row.Model,
            row.Calls,
            row.PartialCalls,
            row.FailedCalls,
            row.PromptTokens,
            row.CompletionTokens,
            row.TotalTokens,
            row.Duration,
            row.Slowest,
            row.BalanceSpent);
}
