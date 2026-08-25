namespace ChatNode.Api.Rest.Messages.Documents;

public record DocumentResponse(
    Guid Id,
    string Kind,
    string? FileName,
    long SizeBytes,
    Guid OwnerId,
    Guid? ChatId,
    bool ContainsPersonalData,
    DateTimeOffset CreatedAt,
    DocumentReviewResponse? Review);

public record DocumentReviewResponse(
    string Stage,
    string? Detail,
    Guid? ChatId,
    string? MessageId,
    int? TotalScore,
    int? MaxScore,
    int? UnverifiedCount,
    DateTimeOffset? CompletedAt);

public record DocumentListResponse(IReadOnlyList<DocumentResponse> Items, int Total);

public record DeleteDocumentResponse(
    Guid DocumentId,
    string Kind,
    string FileName,
    bool ManualRemoved,
    string Message);
