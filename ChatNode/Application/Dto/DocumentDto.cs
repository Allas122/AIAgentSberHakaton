using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record DocumentDto(
    Guid Id,
    StoredDocumentKind Kind,
    string? FileName,
    long SizeBytes,
    Guid OwnerId,
    Guid? ChatId,
    bool ContainsPersonalData,
    DateTimeOffset CreatedAt,
    DocumentReviewDto? Review);

public record DocumentReviewDto(
    string Stage,
    string? Detail,
    Guid? ChatId,
    string? MessageId,
    int? TotalScore,
    int? MaxScore,
    int? UnverifiedCount,
    DateTimeOffset? CompletedAt);

public record DocumentPageDto(IReadOnlyList<DocumentDto> Items, int Total);

public record DocumentContentDto(Stream Content, string FileName, string ContentType);

public record DocumentDeletionDto(
    Guid DocumentId,
    StoredDocumentKind Kind,
    string FileName,
    bool ManualRemoved,
    string Message);
