using System.Text.Json;
using ChatNode.Infrastructure.AI.Review;
using ChatNode.Infrastructure.Database;
using ChatNode.Infrastructure.Database.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Repositories;

public class ReviewRepository(AppDbContext context, ILogger<ReviewRepository> logger) : IReviewRepository
{
    public async Task SaveAsync(
        Guid chatId,
        Guid ownerId,
        Guid manualId,
        Guid? documentId,
        string? messageId,
        string? fileName,
        ApplicationReviewResult review,
        CancellationToken ct = default)
    {
        try
        {
            var stored = new StoredReview
            {
                Id = Guid.NewGuid(),
                ChatId = chatId,
                OwnerId = ownerId,
                ManualId = manualId,
                DocumentId = documentId,
                MessageId = messageId,
                FileName = fileName,
                TotalScore = review.TotalScore,
                MaxScore = review.MaxScore,
                UnverifiedCount = review.UnverifiedCount,
                CreatedAt = DateTimeOffset.UtcNow,
                Criteria = review.Criteria.Select(outcome => new StoredReviewCriterion
                {
                    Id = Guid.NewGuid(),
                    Index = outcome.Criterion.Index,
                    Name = outcome.Criterion.Name,
                    Status = outcome.Status,
                    Score = outcome.Score,
                    MaxScore = outcome.Criterion.MaxScore,
                    Explanation = outcome.Explanation,
                    Findings = JsonSerializer.Serialize(
                        outcome.Findings.Select(f => new { f.Content, Type = f.Type.ToString(), Source = f.Source.ToString() }))
                }).ToList()
            };

            context.Reviews.Add(stored);
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось сохранить разбор заявки чата {ChatId}", chatId);
        }
    }

    public async Task<IReadOnlyList<StoredReview>> ListByDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return [];

        var reviews = await context.Reviews
            .AsNoTracking()
            .Where(x => x.DocumentId != null && documentIds.Contains(x.DocumentId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return reviews
            .GroupBy(x => x.DocumentId!.Value)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<StoredReview?> GetLatestAsync(Guid chatId, CancellationToken ct = default) =>
        await context.Reviews
            .AsNoTracking()
            .Include(x => x.Criteria)
            .Where(x => x.ChatId == chatId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<StoredReview?> GetLatestForOwnerAsync(Guid ownerId, CancellationToken ct = default) =>
        await context.Reviews
            .AsNoTracking()
            .Include(x => x.Criteria)
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task RemoveByChatAsync(Guid chatId, CancellationToken ct = default) =>
        await context.Reviews.Where(x => x.ChatId == chatId).ExecuteDeleteAsync(ct);

    public async Task RemoveByDocumentAsync(Guid documentId, CancellationToken ct = default) =>
        await context.Reviews.Where(x => x.DocumentId == documentId).ExecuteDeleteAsync(ct);
}
