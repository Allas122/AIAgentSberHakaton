using Domain.Entities;
using Domain.ValueTypes;

namespace Domain.Repositories;

public interface IAssignmentRepository
{
    Task<Guid> CreateAsync(Assignment assignment);

    Task<Assignment?> GetAsync(Guid assignmentId);

    Task<bool> UpdateAsync(Assignment assignment);

    Task<bool> DeleteAsync(Guid assignmentId);

    Task<IReadOnlyList<Assignment>> ListAsync(Guid ownerId, AssignmentStatus? status, int limit, int offset);

    Task<IReadOnlyList<Assignment>> ListAllAsync(AssignmentStatus? status, int limit, int offset);

    Task<int> CountAsync(Guid? ownerId, AssignmentStatus? status);
}
