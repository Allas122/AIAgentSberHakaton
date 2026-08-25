using System.Text.Json.Serialization;
using Domain.ValueTypes;

namespace ChatNode.Api.Rest.Messages.Accounts;

public record AccountResponse(
    Guid Id,
    string Login,
    string Name,
    string? Email,
    string Role,
    bool IsSelf,
    DateTimeOffset CreatedAt);

public record CreateAccountRequest(
    string Login,
    string Password,
    string Name,
    string? Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
    UserRole Role);

public record RegisterRequest(
    string Login,
    string Password,
    string Name,
    string? Email);

public record UpdateAccountRequest(
    string? Name,
    string? Email,
    [property: JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
    UserRole? Role,
    string? Password);
