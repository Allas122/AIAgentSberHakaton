using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public class ManualDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public ManualStage Stage { get; set; }
    public int TotalChunks { get; set; }
    public int ProcessedChunks { get; set; }
    public int FailedChunks { get; set; }
    public string? Detail { get; set; }
}

public record ManualUploadDto(
    Guid ManualId,
    string Title,
    ManualStage Stage,
    long QueueDepth,
    string Message);
