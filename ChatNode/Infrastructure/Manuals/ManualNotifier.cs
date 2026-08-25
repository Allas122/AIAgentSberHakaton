using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Manuals;

public record ManualStatusUpdate(
    Guid ManualId,
    string Title,
    ManualStage Stage,
    int TotalChunks,
    int ProcessedChunks,
    int FailedChunks,
    string? Detail,
    IReadOnlyList<string> Gaps);

public interface IManualNotifier
{
    Task ManualStatusAsync(Guid ownerId, ManualStatusUpdate update);
}
