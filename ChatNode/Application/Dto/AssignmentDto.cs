using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record AssignmentDto(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Description,
    string? Assignee,
    Guid? AssigneeId,
    DateTimeOffset? DueDate,
    AssignmentStatus Status,
    AssignmentSource SourceKind,
    string? SourceRef,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CreateAssignmentDto(
    string Title,
    string Description,
    string? Assignee,
    Guid? AssigneeId,
    DateTimeOffset? DueDate,
    AssignmentSource SourceKind,
    string? SourceRef);

public record UpdateAssignmentDto(
    string? Title,
    string? Description,
    string? Assignee,
    Guid? AssigneeId,
    bool ClearAssigneeId,
    DateTimeOffset? DueDate,
    AssignmentStatus? Status);

public record AssignmentPageDto(IReadOnlyList<AssignmentDto> Items, int Total);

public record LetterAssignmentDto(Guid Id, string Title, string? Assignee, string? DueDate, string Status);

public record LetterReplyDto(
    string Reply,
    IReadOnlyList<LetterAssignmentDto> Assignments,
    IReadOnlyList<string> Warnings,
    Guid? DocumentId,
    string? FileName);

public record DocumentFileDto(byte[] Content, string FileName);
