using ChatNode.Infrastructure.AI.Review;
using ChatNode.Infrastructure.Database.Entities;

namespace Domain.Repositories;

public interface IReviewRepository
{
    Task SaveAsync(
        Guid chatId,
        Guid ownerId,
        Guid manualId,
        Guid? documentId,
        string? messageId,
        string? fileName,
        ApplicationReviewResult review,
        CancellationToken ct = default);

    Task<IReadOnlyList<StoredReview>> ListByDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken ct = default);

    Task<StoredReview?> GetLatestAsync(Guid chatId, CancellationToken ct = default);

    Task<StoredReview?> GetLatestForOwnerAsync(Guid ownerId, CancellationToken ct = default);

    Task RemoveByChatAsync(Guid chatId, CancellationToken ct = default);

    Task RemoveByDocumentAsync(Guid documentId, CancellationToken ct = default);
}
