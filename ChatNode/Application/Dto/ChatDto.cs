using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record ChatDto(Guid Id, Guid UserId, string Title, ChatKind Kind);