using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class AssignmentService(
    IAssignmentRepository repository,
    IUserRepository userRepository) : IAssignmentService
{
    public const int MaxLimit = 100;
    public const int DefaultLimit = 50;

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    public async Task<AssignmentPageDto> ListAsync(
        Guid userId,
        UserRole role,
        AssignmentStatus? status,
        int limit,
        int offset)
    {
        DenyNotStaff(role);

        var take = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var skip = Math.Max(offset, 0);

        var items = IsStaff(role)
            ? await repository.ListAllAsync(status, take, skip)
            : await repository.ListAsync(userId, status, take, skip);

        var total = await repository.CountAsync(IsStaff(role) ? null : userId, status);

        return new AssignmentPageDto(items.Select(Map).ToList(), total);
    }

    public async Task<AssignmentDto> GetAsync(Guid userId, UserRole role, Guid assignmentId) =>
        Map(await LoadAsync(userId, role, assignmentId));

    public async Task<AssignmentDto> CreateAsync(Guid userId, UserRole role, CreateAssignmentDto dto)
    {
        DenyNotStaff(role);

        var title = dto.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidRequestException("У поручения должна быть формулировка.");
        }

        var curator = await ResolveCuratorAsync(dto.AssigneeId);

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Title = title,
            Description = dto.Description.Trim(),
            Assignee = curator?.Name ?? Normalize(dto.Assignee),
            AssigneeId = curator?.Id,
            DueDate = dto.DueDate,
            Status = AssignmentStatus.New,
            SourceKind = dto.SourceKind,
            SourceRef = Normalize(dto.SourceRef),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await repository.CreateAsync(assignment);

        return Map(assignment);
    }

    public async Task<AssignmentDto> UpdateAsync(
        Guid userId,
        UserRole role,
        Guid assignmentId,
        UpdateAssignmentDto dto)
    {
        var assignment = await LoadAsync(userId, role, assignmentId);

        if (dto.Title is not null) assignment.Title = dto.Title.Trim();
        if (dto.Description is not null) assignment.Description = dto.Description.Trim();
        if (dto.Assignee is not null) assignment.Assignee = Normalize(dto.Assignee);
        if (dto.DueDate is not null) assignment.DueDate = dto.DueDate;
        if (dto.Status is not null) assignment.Status = dto.Status.Value;

        if (dto.ClearAssigneeId)
        {
            assignment.AssigneeId = null;
            if (dto.Assignee is null) assignment.Assignee = null;
        }
        else if (await ResolveCuratorAsync(dto.AssigneeId) is { } curator)
        {
            assignment.AssigneeId = curator.Id;
            assignment.Assignee = curator.Name;
        }

        if (!await repository.UpdateAsync(assignment))
        {
            throw new NotFoundException("Поручение не найдено.");
        }

        assignment.UpdatedAt = DateTimeOffset.UtcNow;

        return Map(assignment);
    }

    public async Task DeleteAsync(Guid userId, UserRole role, Guid assignmentId)
    {
        await LoadAsync(userId, role, assignmentId);

        if (!await repository.DeleteAsync(assignmentId))
        {
            throw new NotFoundException("Поручение не найдено.");
        }
    }

    private async Task<Assignment> LoadAsync(Guid userId, UserRole role, Guid assignmentId)
    {
        DenyNotStaff(role);

        var assignment = await repository.GetAsync(assignmentId)
                         ?? throw new NotFoundException("Поручение не найдено.");

        if (assignment.OwnerId != userId && !IsStaff(role))
        {
            throw new PermissionDenied("Это поручение принадлежит другому пользователю.");
        }

        return assignment;
    }

    private async Task<UserAccount?> ResolveCuratorAsync(Guid? assigneeId)
    {
        if (assigneeId is not { } id || id == Guid.Empty) return null;

        var account = await userRepository.FindAccountByIdAsync(id)
                      ?? throw new NotFoundException("Такой учётной записи нет — некому поручать.");

        if (!IsStaff(account.Role))
        {
            throw new InvalidRequestException(
                $"«{account.Name}» не куратор и не проректор — поручение можно назначить только сотруднику.");
        }

        return account;
    }

    private static void DenyNotStaff(UserRole role)
    {
        if (!IsStaff(role))
        {
            throw new PermissionDenied("Поручения доступны только проректору и координатору.");
        }
    }

    private static bool IsStaff(UserRole role) => StaffRoles.Contains(role);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AssignmentDto Map(Assignment assignment) =>
        new(assignment.Id,
            assignment.OwnerId,
            assignment.Title,
            assignment.Description,
            assignment.Assignee,
            assignment.AssigneeId,
            assignment.DueDate,
            assignment.Status,
            assignment.SourceKind,
            assignment.SourceRef,
            assignment.CreatedAt,
            assignment.UpdatedAt);
}
