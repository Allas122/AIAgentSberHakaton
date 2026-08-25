using ChatNode.Infrastructure.AI.Agents;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Manuals;

public class ManualProgress(
    IManualRepository manualRepository,
    IManualNotifier notifier,
    ILogger<ManualProgress> logger)
{
    private const int MaxListedGaps = 5;

    public Task QueuedAsync(ManualJob job, long queueDepth)
    {
        var detail = queueDepth > 1
            ? $"Файл сохранён в хранилище, методичка в очереди на разбор, перед ней: {queueDepth - 1}"
            : "Файл сохранён в хранилище, начинаю разбор";

        return TrackAsync(job, ManualStage.Queued, 0, 0, 0, detail, []);
    }

    public Task ParsingAsync(ManualJob job, ManualParseProgress progress)
    {
        var detail = progress.TotalChunks == 0
            ? "Готовлю текст методички к разбору"
            : $"Разбираю методичку: {progress.ProcessedChunks} из {progress.TotalChunks} фрагментов" +
              (progress.FailedChunks > 0 ? $", не поддались: {progress.FailedChunks}" : string.Empty);

        return TrackAsync(
            job,
            ManualStage.Parsing,
            progress.TotalChunks,
            progress.ProcessedChunks,
            progress.FailedChunks,
            detail,
            []);
    }

    public Task CompletedAsync(ManualJob job, ManualParseReport report)
    {
        var gaps = report.Gaps.Select(gap => gap.Preview).ToList();

        if (report.Complete)
        {
            return TrackAsync(
                job,
                ManualStage.Ready,
                report.TotalChunks,
                report.ProcessedChunks,
                0,
                $"Методичка разобрана полностью: {report.ProcessedChunks} из {report.TotalChunks} фрагментов",
                gaps);
        }

        var missed = report.TotalChunks - report.ProcessedChunks;

        if (report.ProcessedChunks == 0)
        {
            return TrackAsync(
                job,
                ManualStage.Failed,
                report.TotalChunks,
                0,
                missed,
                NothingParsedDetail(report),
                gaps);
        }

        return TrackAsync(
            job,
            ManualStage.Partial,
            report.TotalChunks,
            report.ProcessedChunks,
            missed,
            PartialDetail(report, missed),
            gaps);
    }

    public Task FailedAsync(ManualJob job, string reason) =>
        TrackAsync(job, ManualStage.Failed, 0, 0, 0, reason, []);

    private static string NothingParsedDetail(ManualParseReport report)
    {
        var cause = report.Gaps.Select(gap => gap.Reason).FirstOrDefault();
        var because = cause is null ? string.Empty : $" Причина: {cause}.";

        return $"Методичку разобрать не удалось: ни один из {report.TotalChunks} фрагментов не попал " +
               $"в базу знаний.{because} Файл сохранён в хранилище — загрузите положение заново, " +
               "по этой методичке заявки проверять нельзя.";
    }

    private static string PartialDetail(ManualParseReport report, int missed)
    {
        var listed = report.Gaps
            .Select(gap => gap.Preview)
            .Where(preview => preview.Length > 0)
            .Take(MaxListedGaps)
            .ToList();

        var tail = report.Gaps.Count > listed.Count
            ? $" и ещё {report.Gaps.Count - listed.Count}"
            : string.Empty;

        var fragments = listed.Count > 0
            ? $" Не попали в базу знаний: {string.Join("; ", listed.Select(x => $"«{x}»"))}{tail}."
            : string.Empty;

        var cause = report.Gaps
            .Select(gap => gap.Reason)
            .FirstOrDefault();

        var because = cause is null ? string.Empty : $" Причина: {cause}.";

        return $"Методичка разобрана частично: {report.ProcessedChunks} из {report.TotalChunks} фрагментов, " +
               $"не удалось разобрать {missed}.{fragments}{because} " +
               "Проверьте эти разделы вручную или загрузите положение повторно.";
    }

    private async Task TrackAsync(
        ManualJob job,
        ManualStage stage,
        int total,
        int processed,
        int failed,
        string? detail,
        IReadOnlyList<string> gaps)
    {
        try
        {
            var alive = await manualRepository.SetManualStatusAsync(
                new ManualStatus(job.ManualId, stage, total, processed, failed, detail));

            if (!alive)
            {
                logger.LogInformation("Методичка {Title} удалена во время разбора, статус не рассылаю", job.Title);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось сохранить состояние разбора методички {Title}", job.Title);
        }

        try
        {
            await notifier.ManualStatusAsync(
                job.OwnerId,
                new ManualStatusUpdate(job.ManualId, job.Title, stage, total, processed, failed, detail, gaps));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось доставить статус разбора методички {Title}", job.Title);
        }
    }
}
