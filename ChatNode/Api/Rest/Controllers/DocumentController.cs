using ChatNode.Api.Configuration;
using ChatNode.Api.Rest.Mappers;
using ChatNode.Api.Rest.Messages.Documents;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Tools;
using Domain.ValueTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatNode.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentController(IDocumentService documentService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<DocumentListResponse> List(
        [FromQuery] StoredDocumentKind? kind,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var page = await documentService.ListAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            kind,
            limit,
            offset,
            ct);

        return page.MapToResponse();
    }

    [HttpGet("{documentId:guid}/content")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken ct)
    {
        var file = await documentService.DownloadAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            documentId,
            ct);

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<DeleteDocumentResponse> Delete(Guid documentId, CancellationToken ct)
    {
        var deleted = await documentService.DeleteAsync(
            HttpContext.User.GetUserId(),
            HttpContext.User.GetUserRole(),
            documentId,
            ct);

        return deleted.MapToResponse();
    }
}
