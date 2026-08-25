using Domain.ValueTypes;

namespace Domain.Entities;

public record Chat(Guid Id, Guid UserId, string Title, ChatKind Kind = ChatKind.Grant);
