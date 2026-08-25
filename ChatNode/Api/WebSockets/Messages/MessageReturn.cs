using Domain.ValueTypes;

namespace ChatNode.Api.WebSockets.Messages;

public record MessageFileReturn(Guid DocumentId, string FileName, string? ReviewMessageId);

public record MessageReturn
(
    string Id,
    string SenderName,
    UserRole SenderRole,
    string Content,
    MessageFileReturn? File = null
);
