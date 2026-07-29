using Domain.ValueTypes;

namespace Domain.Entities;

public record User(Guid Id, UserRole Role, string Name, string? Email, string? HashedPassword);