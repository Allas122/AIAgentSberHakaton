using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Database.Entities;

public class StoredDocument
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public StoredDocumentKind Kind { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ChatId { get; set; }
    public string? FileName { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
