using ChatNode.Api.Rest.Messages.Letters;
using ChatNode.Application.DTO;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Rector))]
[Route("api/letters")]
public class LetterController(ILetterService letterService) : ControllerBase
{
    [HttpPost("reply")]
    [Consumes("multipart/form-data")]
    public async Task<ComposeLetterResponse> ComposeReply(
        [FromForm] ComposeLetterRequest request,
        CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();

        await using var fileStream = request.File?.OpenReadStream();

        var result = await letterService.ComposeReplyAsync(
            new ComposeLetterDto(
                userId,
                fileStream,
                request.File?.FileName,
                request.Text,
                request.Intent,
                request.TemplateId,
                new LetterAddresseeDto(
                    request.AddresseeName,
                    request.AddresseePosition,
                    request.AddresseeSalutation,
                    request.AddresseeGender),
                new LetterRequisitesDto(
                    request.OutgoingNumber,
                    request.OutgoingDate,
                    request.ReplyToNumber,
                    request.ReplyToDate)),
            ct);

        return new ComposeLetterResponse(
            result.Reply,
            result.Assignments
                .Select(a => new ComposedAssignment(a.Id, a.Title, a.Assignee, a.DueDate, a.Status))
                .ToList(),
            result.Warnings,
            result.DocumentId,
            result.FileName);
    }
}
