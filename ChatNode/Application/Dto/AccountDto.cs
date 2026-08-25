using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record AccountDto(
    Guid Id,
    string Login,
    string Name,
    string? Email,
    UserRole Role,
    bool IsSelf,
    DateTimeOffset CreatedAt);

public record CreateAccountDto(string Login, string Password, string Name, string? Email, UserRole Role);

public record RegisterAccountDto(string Login, string Password, string Name, string? Email);

public record UpdateAccountDto(string? Name, string? Email, UserRole? Role, string? Password);
