using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;

namespace ChatNode.Infrastructure.Review;

public class ReviewWorker(
    IServiceScopeFactory scopeFactory,
    IReviewQueue queue,
    ReviewProgress progress,
    ILogger<ReviewWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

    private readonly string _consumer = $"reviewer-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Разборщик заявок {Consumer} запущен", _consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queued = await queue.DequeueAsync(_consumer, stoppingToken)
                             ?? await queue.ReclaimAsync(_consumer, StaleAfter, stoppingToken);

                if (queued is null)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                    continue;
                }

                await HandleAsync(queued, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Очередь разбора недоступна, повторю через {Delay}", IdleDelay);
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task HandleAsync(QueuedReviewJob queued, CancellationToken ct)
    {
        var job = queued.Job;

        using var scope = scopeFactory.CreateScope();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

        await progress.StageAsync(job, ReviewProgress.Reviewing);

        try
        {
            var review = await chatService.RunApplicationReviewAsync(
                job,
                status => progress.StatusAsync(job, status),
                ct);

            await queue.AcknowledgeAsync(queued.EntryId);

            await progress.ReadyAsync(job, review.Id, review.Content);

            logger.LogInformation("Заявка {FileName} разобрана, чат {ChatId}", job.FileName, job.ChatId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Разбор заявки {FileName} прерван остановкой сервиса", job.FileName);
            throw;
        }
        catch (Exception ex)
        {
            await FailAsync(chatService, queued, ex, ct);
        }
    }

    private async Task FailAsync(
        IChatService chatService,
        QueuedReviewJob queued,
        Exception ex,
        CancellationToken ct)
    {
        var job = queued.Job;

        var permanent = ex is InvalidDocumentException or NotFoundException or FileNotFoundException;
        var lastAttempt = queued.Attempt >= ValkeyReviewQueue.MaxAttempts;

        if (!permanent && !lastAttempt)
        {
            logger.LogWarning(
                ex, "Разбор заявки {FileName} сорвался на попытке {Attempt}, вернётся в очередь",
                job.FileName, queued.Attempt);

            return;
        }

        logger.LogError(ex, "Разбор заявки {FileName} провалился окончательно", job.FileName);

        await queue.AcknowledgeAsync(queued.EntryId);

        var reason = permanent
            ? ex.Message
            : "Сервис проверки не ответил. Попробуйте загрузить документ ещё раз.";

        try
        {
            var message = await chatService.ReportReviewFailureAsync(job, reason, ct);

            await progress.FailedAsync(job, message.Id, reason);
        }
        catch (Exception notifyError)
        {
            logger.LogError(notifyError, "Не удалось сообщить пользователю о провале разбора {FileName}", job.FileName);
        }
    }
}
