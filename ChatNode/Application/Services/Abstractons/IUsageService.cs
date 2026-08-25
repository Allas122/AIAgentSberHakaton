using ChatNode.Application.DTO;
using Domain.ValueTypes;

namespace ChatNode.Application.Services.Abstractons;

public interface IUsageService
{
    Task<UsageReportDto> ReportAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);

    Task<UsageRecordPageDto> RecordsAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        TokenOperation? operation,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<int> ClearAsync(
        UserRole role,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
