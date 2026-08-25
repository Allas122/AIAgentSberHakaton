namespace Domain.Entities;

public record Message(
    string Id,
    string Content,
    Guid SenderId,
    DateTime CreateAt,
    Guid? DocumentId = null,
    string? FileName = null);
