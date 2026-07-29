namespace Domain.Entities;

public record ManualPart(
    Guid Id,
    Guid ManualId,
    string Title,
    string Content,
    string Navigation
    );