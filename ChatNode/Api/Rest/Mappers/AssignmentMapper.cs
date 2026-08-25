using ChatNode.Api.Rest.Messages.Assignments;
using ChatNode.Application.DTO;

namespace ChatNode.Api.Rest.Mappers;

public static class AssignmentMapper
{
    public static AssignmentResponse MapToResponse(this AssignmentDto dto) =>
        new(dto.Id,
            dto.OwnerId,
            dto.Title,
            dto.Description,
            dto.Assignee,
            dto.AssigneeId,
            dto.DueDate,
            dto.Status.ToString(),
            dto.SourceKind.ToString(),
            dto.SourceRef,
            dto.CreatedAt,
            dto.UpdatedAt);

    public static AssignmentListResponse MapToResponse(this AssignmentPageDto page) =>
        new(page.Items.Select(MapToResponse).ToList(), page.Total);
}
