using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Analytics;
using ChatNode.Infrastructure.Manuals;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Tools.Documents;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using Microsoft.Extensions.Options;

namespace ChatNode.Application.Services;

public class ManualService(
    AgentFactory agentFactory,
    IManualRepository manualRepository,
    IFileStorage fileStorage,
    IDocumentRegistry documentRegistry,
    IManualQueue manualQueue,
    ManualProgress manualProgress,
    IKnowledgeDocumentReader knowledgeDocumentReader,
    IDatasetRepository datasetRepository,
    IOptions<ManualUploadOptions> uploadOptions,
    ILogger<ManualService> logger
    ) : IManualService
{
    public async Task<ManualUploadDto> QueueManualAsync(
        string title,
        Guid ownerId,
        Stream fileStream,
        string fileName,
        ManualScope scope,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();

        if (content.Length == 0)
        {
            throw new InvalidDocumentException("Файл пустой.");
        }

        if (!knowledgeDocumentReader.Supports(fileName))
        {
            throw new InvalidDocumentException(
                "Формат не поддерживается. Принимаются " +
                string.Join(", ", knowledgeDocumentReader.SupportedExtensions) + ".");
        }

        var manualId = await manualRepository.CreateManualAsync(
            new Manual(Guid.NewGuid(), title, "", ManualStage.Queued, Scope: scope));

        var storageKey = StorageKeys.Manual(manualId);

        await fileStorage.UploadAsync(storageKey, content, cancellationToken);

        await documentRegistry.RecordAsync(
            storageKey,
            StoredDocumentKind.Manual,
            ownerId,
            null,
            fileName,
            content.Length,
            cancellationToken);

        var job = new ManualJob(manualId, ownerId, title, storageKey, fileName);

        await manualQueue.EnqueueAsync(job, cancellationToken);

        var queueDepth = await manualQueue.PendingAsync();

        await manualProgress.QueuedAsync(job, queueDepth);

        var message = queueDepth > 1
            ? $"«{title}» сохранена в хранилище и ждёт разбора, перед ней в очереди: {queueDepth - 1}. " +
              "О ходе разбора сообщу отдельно."
            : $"«{title}» сохранена в хранилище, начинаю разбор. О ходе разбора сообщу отдельно.";

        return new ManualUploadDto(manualId, title, ManualStage.Queued, queueDepth, message);
    }

    public async Task<ManualParseReport> ParseManualAsync(
        ManualJob job,
        Func<ManualParseProgress, Task> onProgress,
        CancellationToken cancellationToken)
    {
        if (await manualRepository.GetManualAsync(job.ManualId) is null)
        {
            throw new NotFoundException("Документ удалён до начала разбора.");
        }

        await using var stream = await fileStorage.DownloadAsync(job.StorageKey, cancellationToken);

        var document = knowledgeDocumentReader.Read(stream, job.FileName);

        await StoreDatasetsAsync(job, document.Tables, cancellationToken);

        var textContent = document.Markdown;

        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidDocumentException(
                "Из файла не удалось извлечь текст — он пуст, состоит из картинок или это скан без текстового слоя.");
        }

        textContent = Cap(textContent, job);

        var manualParserAgent = agentFactory.CreateManualParserAgent(job.ManualId);

        return await manualParserAgent.ParseAsync(textContent, onProgress, cancellationToken);
    }

    private string Cap(string text, ManualJob job)
    {
        var max = uploadOptions.Value.MaxTextChars;

        if (max <= 0 || text.Length <= max) return text;

        var cut = text.LastIndexOf('\n', max - 1);
        var capped = text[..(cut > max / 2 ? cut : max)];

        logger.LogWarning(
            "Документ {Title}: текст обрезан с {Original} до {Capped} символов, разбор пойдёт по началу документа",
            job.Title,
            text.Length,
            capped.Length);

        return capped;
    }

    private async Task StoreDatasetsAsync(ManualJob job, IReadOnlyList<SourceTable> tables, CancellationToken ct)
    {
        if (tables.Count == 0) return;

        await datasetRepository.DeleteByManualAsync(job.ManualId, ct);

        foreach (var table in tables)
        {
            if (table.Header.Count == 0 || table.Rows.Count == 0) continue;

            await datasetRepository.SaveAsync(DatasetFactory.Create(job, table), ct);
        }
    }

    public async Task<IEnumerable<ManualDto>> GetManuals(bool includeStaff)
    {
        var manuals = await manualRepository.GetManualsAsync(includeStaff ? null : ManualScope.Public);

        return manuals.Select(manual => manual.MapToManualDto()).ToList();
    }

    public async Task<ManualDto?> GetManual(Guid id, bool includeStaff)
    {
        var manual = await manualRepository.GetManualAsync(id);

        if (manual is null || (manual.Scope == ManualScope.Staff && !includeStaff)) return null;

        return manual.MapToManualDto();
    }
}
