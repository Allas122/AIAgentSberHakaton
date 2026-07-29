using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record UserDto(Guid Id, UserRole Role, string Name, string? Email, string? HashedPassword);