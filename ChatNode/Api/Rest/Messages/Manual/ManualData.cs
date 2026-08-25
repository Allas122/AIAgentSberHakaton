namespace ChatNode.Api.Rest.Messages.Manual;

public class ManualData
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Stage { get; set; }
    public int TotalChunks { get; set; }
    public int ProcessedChunks { get; set; }
    public int FailedChunks { get; set; }
    public string? Detail { get; set; }
}

public record UploadManualResponse(
    Guid ManualId,
    string Title,
    string Stage,
    long QueueDepth,
    string Message);
