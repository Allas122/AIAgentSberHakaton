using Domain.ValueTypes;

namespace Domain.Entities;

public record Assignment
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Assignee { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public AssignmentStatus Status { get; set; }
    public AssignmentSource SourceKind { get; set; }
    public string? SourceRef { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
