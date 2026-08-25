using System.Text.Json.Serialization;
using Domain.ValueTypes;

namespace ChatNode.Api.Rest.Messages.Assignments;

public record AssignmentResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Description,
    string? Assignee,
    Guid? AssigneeId,
    DateTimeOffset? DueDate,
    string Status,
    string SourceKind,
    string? SourceRef,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record AssignmentListResponse(IReadOnlyList<AssignmentResponse> Items, int Total);

public record CreateAssignmentRequest(
    string Title,
    string? Description,
    string? Assignee,
    Guid? AssigneeId,
    DateTimeOffset? DueDate);

public record UpdateAssignmentRequest(
    string? Title,
    string? Description,
    string? Assignee,
    Guid? AssigneeId,
    DateTimeOffset? DueDate,
    [property: JsonConverter(typeof(JsonStringEnumConverter<AssignmentStatus>))]
    AssignmentStatus? Status);
