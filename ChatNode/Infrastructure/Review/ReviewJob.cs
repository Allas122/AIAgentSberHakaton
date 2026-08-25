using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Review;

public record ReviewJob(
    Guid ChatId,
    Guid UserId,
    Guid ManualId,
    Guid ApplicationId,
    Guid? DocumentId,
    string StorageKey,
    string FileName,
    string? Comment,
    ContestKind ContestKind);

public record QueuedReviewJob(string EntryId, ReviewJob Job, int Attempt);
