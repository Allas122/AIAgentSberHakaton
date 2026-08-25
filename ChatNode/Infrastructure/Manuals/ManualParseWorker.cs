using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;

namespace ChatNode.Infrastructure.Manuals;

public class ManualParseWorker(
    IServiceScopeFactory scopeFactory,
    IManualQueue queue,
    ILogger<ManualParseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly string _consumer = $"manual-parser-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Разборщик методичек {Consumer} запущен", _consumer);

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
                logger.LogError(ex, "Очередь разбора методичек недоступна, повторю через {Delay}", IdleDelay);
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }

    private async Task HandleAsync(QueuedManualJob queued, CancellationToken ct)
    {
        var job = queued.Job;

        using var scope = scopeFactory.CreateScope();
        var manualService = scope.ServiceProvider.GetRequiredService<IManualService>();
        var progress = scope.ServiceProvider.GetRequiredService<ManualProgress>();

        try
        {
            var report = await manualService.ParseManualAsync(
                job,
                parsed => progress.ParsingAsync(job, parsed),
                ct);

            await queue.AcknowledgeAsync(queued.EntryId);

            await progress.CompletedAsync(job, report);

            logger.LogInformation(
                "Методичка {Title} разобрана: {Processed} из {Total} фрагментов, пропущено {Missed}",
                job.Title, report.ProcessedChunks, report.TotalChunks,
                report.TotalChunks - report.ProcessedChunks);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Разбор методички {Title} прерван остановкой сервиса", job.Title);
            throw;
        }
        catch (Exception ex)
        {
            await FailAsync(progress, queued, ex);
        }
    }

    private async Task FailAsync(ManualProgress progress, QueuedManualJob queued, Exception ex)
    {
        var job = queued.Job;

        var permanent = ex is InvalidDocumentException or NotFoundException or FileNotFoundException;
        var lastAttempt = queued.Attempt >= ValkeyManualQueue.MaxAttempts;

        if (!permanent && !lastAttempt)
        {
            logger.LogWarning(
                ex, "Разбор методички {Title} сорвался на попытке {Attempt}, вернётся в очередь",
                job.Title, queued.Attempt);

            return;
        }

        logger.LogError(ex, "Разбор методички {Title} провалился окончательно", job.Title);

        await queue.AcknowledgeAsync(queued.EntryId);

        var reason = permanent
            ? $"Методичку разобрать не удалось: {ex.Message} Файл сохранён в хранилище — исправьте документ и загрузите его заново."
            : "Методичку разобрать не удалось: сервис разбора не ответил. Файл сохранён в хранилище — попробуйте загрузить положение ещё раз.";

        await progress.FailedAsync(job, reason);
    }
}
