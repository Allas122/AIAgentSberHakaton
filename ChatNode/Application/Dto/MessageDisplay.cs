using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record MessageFileDto(Guid DocumentId, string FileName, string? ReviewMessageId);

public record MessageDisplayDto(
    string Id,
    string Content,
    UserRole UserRole,
    string userName,
    MessageFileDto? File = null);
