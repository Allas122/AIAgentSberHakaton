using ChatNode.Infrastructure.Database.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Storage.Abstractions;

public interface IDocumentRegistry
{
    Task<IReadOnlyList<StoredDocument>> ListAsync(
        Guid? ownerId,
        StoredDocumentKind? kind,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<int> CountAsync(Guid? ownerId, StoredDocumentKind? kind, CancellationToken ct = default);

    Task<StoredDocument?> GetAsync(Guid documentId, CancellationToken ct = default);

    Task<Guid?> RecordAsync(
        string storageKey,
        StoredDocumentKind kind,
        Guid ownerId,
        Guid? chatId,
        string? fileName,
        long sizeBytes,
        CancellationToken ct = default);

    Task RemoveAsync(Guid documentId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListKeysByChatAsync(Guid chatId, CancellationToken ct = default);

    Task RemoveByChatAsync(Guid chatId, CancellationToken ct = default);
}
