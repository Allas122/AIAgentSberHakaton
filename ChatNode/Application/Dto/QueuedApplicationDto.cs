namespace ChatNode.Application.DTO;

public record ActiveReviewDto(
    Guid DocumentId,
    Guid ChatId,
    string FileName,
    string Stage,
    string? Detail);

public record QueuedApplicationDto(
    string MessageId,
    Guid ApplicationId,
    Guid? DocumentId,
    string FileName,
    long QueueDepth);
