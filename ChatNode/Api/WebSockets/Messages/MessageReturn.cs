using ChatNode.Infrastructure.Dto;
using Domain.ValueTypes;

namespace ChatNode.Api.WebSockets.Messages;

public record MessageReturn
(
    string Id,
    string SenderName,
    UserRole SenderRole,
    string Content
);