using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Database.Entities;
using ChatNode.Infrastructure.Storage.Abstractions;
using Domain.ValueTypes;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Storage;

public class DocumentRegistry(AppDbContext context, ILogger<DocumentRegistry> logger) : IDocumentRegistry
{
    public async Task<Guid?> RecordAsync(
        string storageKey,
        StoredDocumentKind kind,
        Guid ownerId,
        Guid? chatId,
        string? fileName,
        long sizeBytes,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await context.Documents.FirstOrDefaultAsync(x => x.StorageKey == storageKey, ct);

            if (existing is not null)
            {
                existing.FileName = fileName;
                existing.SizeBytes = sizeBytes;
                await context.SaveChangesAsync(ct);
                return existing.Id;
            }

            var document = new StoredDocument
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                Kind = kind,
                OwnerId = ownerId,
                ChatId = chatId,
                FileName = fileName,
                SizeBytes = sizeBytes,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Documents.Add(document);

            await context.SaveChangesAsync(ct);

            return document.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось записать метаданные файла {StorageKey}", storageKey);
            return null;
        }
    }

    public async Task<IReadOnlyList<StoredDocument>> ListAsync(
        Guid? ownerId,
        StoredDocumentKind? kind,
        int limit,
        int offset,
        CancellationToken ct = default) =>
        await Filter(ownerId, kind)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(Math.Max(offset, 0))
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

    public async Task<int> CountAsync(Guid? ownerId, StoredDocumentKind? kind, CancellationToken ct = default) =>
        await Filter(ownerId, kind).CountAsync(ct);

    public async Task<StoredDocument?> GetAsync(Guid documentId, CancellationToken ct = default) =>
        await context.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == documentId, ct);

    private IQueryable<StoredDocument> Filter(Guid? ownerId, StoredDocumentKind? kind)
    {
        var query = context.Documents.AsNoTracking();

        if (ownerId is not null) query = query.Where(x => x.OwnerId == ownerId.Value);
        if (kind is not null) query = query.Where(x => x.Kind == kind.Value);

        return query;
    }

    public async Task RemoveAsync(Guid documentId, CancellationToken ct = default) =>
        await context.Documents.Where(x => x.Id == documentId).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<string>> ListKeysByChatAsync(Guid chatId, CancellationToken ct = default) =>
        await context.Documents
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .Select(x => x.StorageKey)
            .ToListAsync(ct);

    public async Task RemoveByChatAsync(Guid chatId, CancellationToken ct = default) =>
        await context.Documents.Where(x => x.ChatId == chatId).ExecuteDeleteAsync(ct);
}
