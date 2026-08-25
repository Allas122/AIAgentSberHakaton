using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record CreateAssignmentArguments(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("assignee")] string? Assignee,
    [property: JsonPropertyName("due_date")] string? DueDate);

public record ListAssignmentsArguments(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("limit")] int Limit);

public record UpdateAssignmentStatusArguments(
    [property: JsonPropertyName("assignment_id")] string AssignmentId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("comment")] string? Comment);
