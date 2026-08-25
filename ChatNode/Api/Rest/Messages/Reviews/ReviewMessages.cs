namespace ChatNode.Api.Rest.Messages.Reviews;

public record ActiveReviewResponse(
    Guid DocumentId,
    Guid ChatId,
    string FileName,
    string Stage,
    string? Detail);
