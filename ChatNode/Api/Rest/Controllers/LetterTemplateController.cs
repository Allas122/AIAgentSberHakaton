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
[Route("api/letter-templates")]
public class LetterTemplateController(ILetterTemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<LetterTemplateResponse>> List()
    {
        var templates = await templateService.ListAsync(HttpContext.User.GetUserId());

        return templates.Select(Map).ToList();
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<LetterTemplateResponse> Create(
        [FromForm] CreateLetterTemplateRequest request,
        CancellationToken ct)
    {
        await using var fileStream = request.File?.OpenReadStream();

        var template = await templateService.CreateAsync(
            HttpContext.User.GetUserId(),
            new CreateLetterTemplateDto(request.Name, request.Content, fileStream, request.File?.FileName),
            ct);

        return Map(template);
    }

    [HttpPatch("{templateId:guid}")]
    public async Task<LetterTemplateResponse> Update(
        Guid templateId,
        [FromBody] UpdateLetterTemplateRequest request)
    {
        var template = await templateService.UpdateAsync(
            HttpContext.User.GetUserId(),
            templateId,
            new UpdateLetterTemplateDto(request.Name, request.Content));

        return Map(template);
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid templateId)
    {
        await templateService.DeleteAsync(HttpContext.User.GetUserId(), templateId);
        return NoContent();
    }

    private static LetterTemplateResponse Map(LetterTemplateDto dto) =>
        new(dto.Id, dto.Name, dto.Content, dto.SourceFileName, dto.CreatedAt, dto.UpdatedAt);
}
