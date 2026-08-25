using ChatNode.Infrastructure.Database;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class AssignmentRepository(AppDbContext context) : IAssignmentRepository
{
    public async Task<Guid> CreateAsync(Assignment assignment)
    {
        var now = DateTimeOffset.UtcNow;

        assignment.Id = assignment.Id == Guid.Empty ? Guid.NewGuid() : assignment.Id;
        assignment.CreatedAt = assignment.CreatedAt == default ? now : assignment.CreatedAt;
        assignment.UpdatedAt = now;

        context.Assignments.Add(assignment);
        await context.SaveChangesAsync();

        return assignment.Id;
    }

    public async Task<Assignment?> GetAsync(Guid assignmentId) =>
        await context.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId);

    public async Task<bool> UpdateAsync(Assignment assignment)
    {
        var stored = await context.Assignments.FirstOrDefaultAsync(x => x.Id == assignment.Id);
        if (stored is null) return false;

        stored.Title = assignment.Title;
        stored.Description = assignment.Description;
        stored.Assignee = assignment.Assignee;
        stored.AssigneeId = assignment.AssigneeId;
        stored.DueDate = assignment.DueDate;
        stored.Status = assignment.Status;
        stored.SourceKind = assignment.SourceKind;
        stored.SourceRef = assignment.SourceRef;
        stored.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid assignmentId) =>
        await context.Assignments.Where(x => x.Id == assignmentId).ExecuteDeleteAsync() > 0;

    public async Task<IReadOnlyList<Assignment>> ListAsync(
        Guid ownerId,
        AssignmentStatus? status,
        int limit,
        int offset) =>
        await Query(ownerId, status, limit, offset).ToListAsync();

    public async Task<IReadOnlyList<Assignment>> ListAllAsync(AssignmentStatus? status, int limit, int offset) =>
        await Query(null, status, limit, offset).ToListAsync();

    public async Task<int> CountAsync(Guid? ownerId, AssignmentStatus? status) =>
        await Filter(ownerId, status).CountAsync();

    private IQueryable<Assignment> Query(Guid? ownerId, AssignmentStatus? status, int limit, int offset) =>
        Filter(ownerId, status)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(Math.Max(offset, 0))
            .Take(Math.Clamp(limit, 1, 200));

    private IQueryable<Assignment> Filter(Guid? ownerId, AssignmentStatus? status)
    {
        var query = context.Assignments.AsNoTracking();

        if (ownerId is not null) query = query.Where(x => x.OwnerId == ownerId.Value);
        if (status is not null) query = query.Where(x => x.Status == status.Value);

        return query;
    }
}
