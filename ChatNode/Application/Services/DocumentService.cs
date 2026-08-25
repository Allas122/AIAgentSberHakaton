using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.Database.Entities;
using ChatNode.Infrastructure.Review;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using Domain.Repositories;
using Domain.ValueTypes;

namespace ChatNode.Application.Services;

public class DocumentService(
    IDocumentRegistry registry,
    IFileStorage fileStorage,
    IReviewRepository reviewRepository,
    IReviewStatusStore reviewStatusStore,
    IManualRepository manualRepository,
    ILogger<DocumentService> logger) : IDocumentService
{
    public const int MaxLimit = 100;
    public const int DefaultLimit = 50;

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    public async Task<DocumentPageDto> ListAsync(
        Guid userId,
        UserRole role,
        StoredDocumentKind? kind,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var take = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var owner = IsStaff(role) ? (Guid?)null : userId;

        var items = await registry.ListAsync(owner, kind, take, Math.Max(offset, 0), ct);
        var total = await registry.CountAsync(owner, kind, ct);

        var applications = items
            .Where(document => document.Kind == StoredDocumentKind.GrantApplication)
            .ToList();

        var reviews = (await reviewRepository.ListByDocumentsAsync(
                applications.Select(document => document.Id).ToList(), ct))
            .ToDictionary(review => review.DocumentId!.Value);

        var running = await RunningAsync(userId, applications.Count > 0);

        return new DocumentPageDto(
            items.Select(document => Map(document, ReviewOf(document, running, reviews))).ToList(),
            total);
    }

    private async Task<IReadOnlyDictionary<Guid, ReviewStatusSnapshot>> RunningAsync(Guid userId, bool needed)
    {
        if (!needed) return new Dictionary<Guid, ReviewStatusSnapshot>();

        try
        {
            return await reviewStatusStore.ListAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Состояние разборов недоступно, отдаю список файлов без него");
            return new Dictionary<Guid, ReviewStatusSnapshot>();
        }
    }

    private static DocumentReviewDto? ReviewOf(
        StoredDocument document,
        IReadOnlyDictionary<Guid, ReviewStatusSnapshot> running,
        IReadOnlyDictionary<Guid, StoredReview> reviews)
    {
        if (document.Kind != StoredDocumentKind.GrantApplication) return null;

        if (running.TryGetValue(document.Id, out var snapshot))
        {
            return new DocumentReviewDto(
                snapshot.Stage, snapshot.Detail, snapshot.ChatId, null, null, null, null, null);
        }

        if (!reviews.TryGetValue(document.Id, out var review)) return null;

        return new DocumentReviewDto(
            ReviewProgress.Reviewed,
            null,
            review.ChatId,
            review.MessageId,
            review.TotalScore,
            review.MaxScore,
            review.UnverifiedCount,
            review.CreatedAt);
    }

    public async Task<DocumentContentDto> DownloadAsync(
        Guid userId,
        UserRole role,
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await registry.GetAsync(documentId, ct)
                       ?? throw new NotFoundException("Файл не найден.");

        if (document.OwnerId != userId && !IsStaff(role))
        {
            throw new PermissionDenied("Этот файл загружал другой пользователь.");
        }

        if (!await fileStorage.ExistsAsync(document.StorageKey, ct))
        {
            throw new NotFoundException("Файл числится в реестре, но в хранилище его нет.");
        }

        var content = await fileStorage.DownloadAsync(document.StorageKey, ct);

        return new DocumentContentDto(content, FileNameOf(document), ContentTypeOf(document));
    }

    public async Task<DocumentDeletionDto> DeleteAsync(
        Guid userId,
        UserRole role,
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await registry.GetAsync(documentId, ct)
                       ?? throw new NotFoundException("Файл не найден.");

        if (document.OwnerId != userId && !IsStaff(role))
        {
            throw new PermissionDenied("Этот файл загружал другой пользователь.");
        }

        var manualRemoved = document.Kind == StoredDocumentKind.Manual && await RemoveManualAsync(document, ct);

        if (document.Kind == StoredDocumentKind.GrantApplication)
        {
            await EnsureNotUnderReviewAsync(document);
            await reviewRepository.RemoveByDocumentAsync(documentId, ct);
        }

        try
        {
            await fileStorage.DeleteAsync(document.StorageKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Файл {StorageKey} не удалился из хранилища, чищу только реестр", document.StorageKey);
        }

        await registry.RemoveAsync(documentId, ct);

        logger.LogInformation(
            "Файл {FileName} ({Kind}) удалён пользователем {UserId}",
            FileNameOf(document), document.Kind, userId);

        return new DocumentDeletionDto(
            documentId,
            document.Kind,
            FileNameOf(document),
            manualRemoved,
            DeletionMessage(document, manualRemoved));
    }

    private async Task<bool> RemoveManualAsync(StoredDocument document, CancellationToken ct)
    {
        if (!StorageKeys.TryParseManual(document.StorageKey, out var manualId)) return false;

        var manual = await manualRepository.GetManualAsync(manualId);
        if (manual is null) return false;

        if (manual.Stage is ManualStage.Queued or ManualStage.Parsing)
        {
            throw new InvalidRequestException(
                $"«{manual.Title}» сейчас разбирается — дождитесь конца разбора и удалите файл после этого.");
        }

        await manualRepository.DeleteManualAsync(manualId);

        return true;
    }

    private async Task EnsureNotUnderReviewAsync(StoredDocument document)
    {
        ReviewStatusSnapshot? snapshot;

        try
        {
            snapshot = (await reviewStatusStore.ListAsync(document.OwnerId)).GetValueOrDefault(document.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Состояние разборов недоступно, удаляю файл без этой проверки");
            return;
        }

        if (snapshot is null) return;

        if (snapshot.Stage is ReviewProgress.Queued or ReviewProgress.Reviewing)
        {
            throw new InvalidRequestException(
                "Заявка сейчас в разборе — дождитесь его конца, иначе разбор оборвётся на середине.");
        }

        await reviewStatusStore.ClearAsync(document.OwnerId, document.Id);
    }

    private static string DeletionMessage(StoredDocument document, bool manualRemoved) => document.Kind switch
    {
        StoredDocumentKind.Manual when manualRemoved =>
            $"«{FileNameOf(document)}» удалена вместе с разобранными разделами.",
        StoredDocumentKind.GrantApplication =>
            $"«{FileNameOf(document)}» удалена из хранилища вместе с разбором.",
        _ => $"«{FileNameOf(document)}» удалён из хранилища."
    };

    private static bool IsStaff(UserRole role) => StaffRoles.Contains(role);

    private static bool HasPersonalData(StoredDocumentKind kind) => kind == StoredDocumentKind.GrantApplication;

    private static string FileNameOf(StoredDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.FileName)) return document.FileName;

        var extension = document.Kind == StoredDocumentKind.Manual ? "md" : "docx";
        return $"document-{document.Id}.{extension}";
    }

    private static string ContentTypeOf(StoredDocument document) =>
        document.Kind == StoredDocumentKind.Manual
            ? "text/markdown; charset=utf-8"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static DocumentDto Map(StoredDocument document, DocumentReviewDto? review) =>
        new(document.Id,
            document.Kind,
            document.FileName,
            document.SizeBytes,
            document.OwnerId,
            document.ChatId,
            HasPersonalData(document.Kind),
            document.CreatedAt,
            review);
}
