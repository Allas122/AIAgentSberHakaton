using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Assignments;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.Staff)]
[Route("api/assignments")]
public class AssignmentController(IAssignmentService assignmentService) : ControllerBase
{
    [HttpGet]
    public async Task<AssignmentListResponse> List(
        [FromQuery] AssignmentStatus? status,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var page = await assignmentService.ListAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            status,
            limit,
            offset);

        return page.MapToResponse();
    }

    [HttpGet("{assignmentId:guid}")]
    public async Task<AssignmentResponse> Get(Guid assignmentId)
    {
        var assignment = await assignmentService.GetAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            assignmentId);

        return assignment.MapToResponse();
    }

    [HttpPost]
    public async Task<AssignmentResponse> Create([FromBody] CreateAssignmentRequest request)
    {
        var assignment = await assignmentService.CreateAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            new CreateAssignmentDto(
                request.Title,
                request.Description ?? string.Empty,
                request.Assignee,
                request.AssigneeId,
                request.DueDate,
                AssignmentSource.Manual,
                null));

        return assignment.MapToResponse();
    }

    [HttpPatch("{assignmentId:guid}")]
    public async Task<AssignmentResponse> Update(Guid assignmentId, [FromBody] UpdateAssignmentRequest request)
    {
        var assignment = await assignmentService.UpdateAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            assignmentId,
            new UpdateAssignmentDto(
                request.Title,
                request.Description,
                request.Assignee,
                request.AssigneeId == Guid.Empty ? null : request.AssigneeId,
                request.AssigneeId == Guid.Empty,
                request.DueDate,
                request.Status));

        return assignment.MapToResponse();
    }

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Delete(Guid assignmentId)
    {
        await assignmentService.DeleteAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            assignmentId);

        return NoContent();
    }
}
