namespace Domain.Entities;

public record Application(
    Guid Id,
    Guid UserId,
    string Navigation
);