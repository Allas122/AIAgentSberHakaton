using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.Manuals;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class ManualService(
    AgentFactory agentFactory,
    IManualRepository manualRepository,
    IFileStorage fileStorage,
    IDocumentRegistry documentRegistry,
    IManualQueue manualQueue,
    ManualProgress manualProgress
    ) : IManualService
{
    public async Task<ManualUploadDto> QueueManualAsync(
        string title,
        Guid ownerId,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();

        if (content.Length == 0)
        {
            throw new InvalidDocumentException("Файл методички пустой.");
        }

        var manualId = await manualRepository.CreateManualAsync(
            new Manual(Guid.NewGuid(), title, "", ManualStage.Queued));

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
            throw new NotFoundException("Методичка удалена до начала разбора.");
        }

        await using var stream = await fileStorage.DownloadAsync(job.StorageKey, cancellationToken);
        using var reader = new StreamReader(stream);
        var textContent = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidDocumentException("Файл методички пуст — разбирать нечего.");
        }

        var manualParserAgent = agentFactory.CreateManualParserAgent(job.ManualId);

        return await manualParserAgent.ParseAsync(textContent, onProgress, cancellationToken);
    }

    public async Task<IEnumerable<ManualDto>> GetManuals()
    {
        var manuals = await manualRepository.GetManualsAsync();

        return manuals.Select(manual => manual.MapToManualDto()).ToList();
    }

    public async Task<ManualDto?> GetManual(Guid id)
    {
        var manual = await manualRepository.GetManualAsync(id);

        return manual?.MapToManualDto();
    }
}
