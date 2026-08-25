using ChatNode.Application.DTO;
using Domain.ValueTypes;

namespace ChatNode.Application.Services.Abstractons;

public interface IDocumentService
{
    Task<DocumentPageDto> ListAsync(
        Guid userId,
        UserRole role,
        StoredDocumentKind? kind,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<DocumentContentDto> DownloadAsync(Guid userId, UserRole role, Guid documentId, CancellationToken ct = default);

    Task<DocumentDeletionDto> DeleteAsync(Guid userId, UserRole role, Guid documentId, CancellationToken ct = default);
}
