using ChatNode.Application.DTO;
using Domain.ValueTypes;

namespace ChatNode.Application.Services.Abstractons;

public interface IAssignmentService
{
    Task<AssignmentPageDto> ListAsync(Guid userId, UserRole role, AssignmentStatus? status, int limit, int offset);

    Task<AssignmentDto> GetAsync(Guid userId, UserRole role, Guid assignmentId);

    Task<AssignmentDto> CreateAsync(Guid userId, UserRole role, CreateAssignmentDto dto);

    Task<AssignmentDto> UpdateAsync(Guid userId, UserRole role, Guid assignmentId, UpdateAssignmentDto dto);

    Task DeleteAsync(Guid userId, UserRole role, Guid assignmentId);
}
