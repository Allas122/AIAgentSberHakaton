namespace ChatNode.Application.DTO;

public record MessageDto(
    string Id,
    string Content,
    Guid SenderId,
    DateTime CreateAt,
    Guid? DocumentId = null,
    string? FileName = null);
