namespace ChatNode.Infrastructure.AI.Functions.ReturnModels;

public record AssignmentView(
    Guid Id,
    string Title,
    string? Assignee,
    string? DueDate,
    string Status);

public record CreateAssignmentReturn(string Status, string Message, Guid AssignmentId);

public record ListAssignmentsReturn(string Status, int Total, IReadOnlyList<AssignmentView> Assignments);

public record AssignmentActionReturn(string Status, string Message);
