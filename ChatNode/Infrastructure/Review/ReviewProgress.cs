namespace ChatNode.Infrastructure.Review;

public class ReviewProgress(IReviewStatusStore store, IReviewNotifier notifier, ILogger<ReviewProgress> logger)
{
    public const string Queued = "Queued";
    public const string Reviewing = "Reviewing";
    public const string Reviewed = "Reviewed";
    public const string Failed = "Failed";

    public Task QueuedAsync(Guid ownerId, Guid chatId, Guid? documentId, string fileName)
        => TrackAsync(ownerId, documentId, chatId, fileName, Queued, null);

    public Task StageAsync(ReviewJob job, string stage)
        => TrackAsync(job.UserId, job.DocumentId, job.ChatId, job.FileName, stage, null);

    public Task StatusAsync(ReviewJob job, string status)
        => TrackAsync(job.UserId, job.DocumentId, job.ChatId, job.FileName, Reviewing, status);

    public async Task ReadyAsync(ReviewJob job, string messageId, string content)
    {
        await ClearAsync(job);

        await NotifyAsync(job.FileName, () => notifier.FileStatusAsync(job.UserId, job.FileName, Reviewed));
        await NotifyAsync(job.FileName, () => notifier.ReviewReadyAsync(job.UserId, job.ChatId, job.DocumentId, messageId, content));
    }

    public async Task FailedAsync(ReviewJob job, string messageId, string reason)
    {
        await ClearAsync(job);

        await NotifyAsync(job.FileName, () => notifier.FileStatusAsync(job.UserId, job.FileName, Failed));
        await NotifyAsync(
            job.FileName,
            () => notifier.ReviewFailedAsync(job.UserId, job.ChatId, job.FileName, messageId, reason));
    }

    private async Task NotifyAsync(string fileName, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось доставить уведомление о разборе {FileName}", fileName);
        }
    }

    private async Task ClearAsync(ReviewJob job)
    {
        if (job.DocumentId is not { } documentId) return;

        try
        {
            await store.ClearAsync(job.UserId, documentId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось снять отметку о разборе {FileName}", job.FileName);
        }
    }

    private async Task TrackAsync(
        Guid ownerId,
        Guid? documentId,
        Guid chatId,
        string fileName,
        string stage,
        string? detail)
    {
        if (documentId is { } id)
        {
            try
            {
                await store.SaveAsync(ownerId, new ReviewStatusSnapshot(id, chatId, fileName, stage, detail));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось сохранить состояние разбора {FileName}", fileName);
            }
        }

        if (detail is not null)
        {
            await NotifyAsync(fileName, () => notifier.StatusAsync(ownerId, chatId, detail));
            return;
        }

        await NotifyAsync(fileName, () => notifier.FileStatusAsync(ownerId, fileName, stage));
    }
}
