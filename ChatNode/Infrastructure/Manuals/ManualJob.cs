namespace ChatNode.Infrastructure.Manuals;

public record ManualJob(
    Guid ManualId,
    Guid OwnerId,
    string Title,
    string StorageKey,
    string FileName);

public record QueuedManualJob(string EntryId, ManualJob Job, int Attempt);
