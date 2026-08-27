using Domain.ValueTypes;

namespace Domain.Entities;

public record Manual(
    Guid Id,
    string Title,
    string Navigation,
    ManualStage Stage = ManualStage.Ready,
    int TotalChunks = 0,
    int ProcessedChunks = 0,
    int FailedChunks = 0,
    string? StatusDetail = null,
    ManualScope Scope = ManualScope.Staff);

public record ManualStatus(
    Guid ManualId,
    ManualStage Stage,
    int TotalChunks,
    int ProcessedChunks,
    int FailedChunks,
    string? Detail);
