using System.Diagnostics;
using ChatNode.Application.DTO;
using ChatNode.Application.Exceptions;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Application.Mappers;
using ChatNode.Application.Services.Abstractons;
using ChatNode.Infrastructure.AI.Agents;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Review;
using ChatNode.Infrastructure.Storage;
using ChatNode.Infrastructure.Storage.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Tools.Documents;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using StackExchange.Redis;

namespace ChatNode.Application.Services;

public class ChatService(
    IChatRepository chatRepository,
    IUserRepository userRepository,
    IManualRepository manualRepository,
    AgentFactory agentFactory,
    IDocxAnonymizer docxAnonymizer,
    IDocxTextExtractor docxTextExtractor,
    IPdfTextExtractor pdfTextExtractor,
    IAnonymizeClient anonymizeClient,
    IFileStorage fileStorage,
    IDocxWriter docxWriter,
    IDocumentRegistry documentRegistry,
    IPinRepository pinRepository,
    IReviewRepository reviewRepository,
    IReviewQueue reviewQueue,
    ReviewProgress reviewProgress,
    IReviewStatusStore reviewStatusStore,
    ITokenMeter tokenMeter,
    BalanceProbe balanceProbe,
    ILogger<ChatService> logger
    ) : IChatService
{
    public async Task<IReadOnlyList<ActiveReviewDto>> GetActiveReviewsAsync(Guid userId)
    {
        try
        {
            var running = await reviewStatusStore.ListAsync(userId);

            return running.Values
                .Select(s => new ActiveReviewDto(s.DocumentId, s.ChatId, s.FileName, s.Stage, s.Detail))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Состояние разборов недоступно, отдаю пустой список");
            return [];
        }
    }

    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 10;

    private const int KeepResultAttempts = 4;

    private static readonly TimeSpan KeepResultDelay = TimeSpan.FromSeconds(3);

    private async Task<T> KeepResultAsync<T>(Func<Task<T>> save, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await save();
            }
            catch (RedisTimeoutException ex) when (attempt < KeepResultAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Valkey не ответил при сохранении готового разбора, попытка {Attempt} из {Total}",
                    attempt,
                    KeepResultAttempts);

                await Task.Delay(KeepResultDelay, ct);
            }
            catch (RedisConnectionException ex) when (attempt < KeepResultAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Связь с Valkey потеряна при сохранении готового разбора, попытка {Attempt} из {Total}",
                    attempt,
                    KeepResultAttempts);

                await Task.Delay(KeepResultDelay, ct);
            }
        }
    }

    private async Task RecordReviewSpendAsync(
        ReviewJob job,
        DateTimeOffset startedAt,
        TimeSpan duration,
        IReadOnlyDictionary<string, double>? balanceBefore,
        CancellationToken ct)
    {
        var spent = BalanceProbe.Spent(balanceBefore, await balanceProbe.SnapshotAsync(ct));

        if (spent.Count == 0)
        {
            await tokenMeter.RecordAsync(
                new TokenRecord(
                    TokenOperation.ApplicationReview,
                    "*",
                    startedAt,
                    duration,
                    ChatId: job.ChatId,
                    UserId: job.UserId),
                ct);

            return;
        }

        foreach (var (model, value) in spent)
        {
            await tokenMeter.RecordAsync(
                new TokenRecord(
                    TokenOperation.ApplicationReview,
                    model,
                    startedAt,
                    duration,
                    BalanceSpent: value,
                    ChatId: job.ChatId,
                    UserId: job.UserId),
                ct);
        }
    }

    private async Task<Chat> TryCheck(Guid chatId, Guid userId)
    {
        var chat = await chatRepository.GetChatAsync(chatId);
        if (chat is null) throw new NotFoundException("Чат не найден.");
        if (chat.UserId != userId) throw new PermissionDenied("Этот чат принадлежит другому пользователю.");

        return chat;
    }
    
    public async Task<Guid> CreateChatAsync(string title, Guid userId, ChatKind kind = ChatKind.Grant)
    {
        if (kind == ChatKind.Staff) await DenyNotStaffAsync(userId);

        var chatId = await chatRepository.CreateChatAsync(title, userId, kind);
        return chatId;
    }

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    private async Task DenyNotStaffAsync(Guid userId)
    {
        var account = await userRepository.FindAccountByIdAsync(userId);

        if (account is null || !StaffRoles.Contains(account.Role))
        {
            throw new PermissionDenied("Служебный чат доступен только проректору и координатору.");
        }
    }

    public async Task<List<MessageDisplayDto>> GetMessagesAsync(Guid chatId,Guid userId, int limit, string? lastMessageId)
    {
        await  TryCheck(chatId, userId);
        var messages = await chatRepository.GetMessagesAsync(chatId, Page(limit), lastMessageId);

        var reviewsByDocument = await ReviewMessageIdsAsync(messages);

        List<MessageDisplayDto> result=[];
        foreach (var m in messages)
        {
            var senderRole = UserRole.Agent;
            var senderName = "Agent";
            if (m.SenderId != Guid.Empty)
            {
                var user = await userRepository.GetUserAsync(m.SenderId);
                senderRole = user?.Role ?? UserRole.User;
                senderName = user?.Name ?? "Пользователь";
            }

            result.Add(new MessageDisplayDto(m.Id, m.Content, senderRole, senderName, FileOf(m, reviewsByDocument)));
        }
        return result;
    }

    private async Task<Dictionary<Guid, string?>> ReviewMessageIdsAsync(IEnumerable<Message> messages)
    {
        var documentIds = messages
            .Where(m => m.DocumentId.HasValue)
            .Select(m => m.DocumentId!.Value)
            .Distinct()
            .ToList();

        if (documentIds.Count == 0) return [];

        try
        {
            var reviews = await reviewRepository.ListByDocumentsAsync(documentIds);

            return reviews
                .Where(r => r.DocumentId.HasValue)
                .ToDictionary(r => r.DocumentId!.Value, r => r.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось выяснить, готовы ли разборы по чату — отдаю вложения без ссылки на разбор");
            return [];
        }
    }

    private static MessageFileDto? FileOf(Message message, IReadOnlyDictionary<Guid, string?> reviews)
    {
        if (message.DocumentId is not { } documentId) return null;

        return new MessageFileDto(
            documentId,
            string.IsNullOrWhiteSpace(message.FileName) ? "Документ" : message.FileName,
            reviews.GetValueOrDefault(documentId));
    }

    public async Task<MessageDisplayDto> SendMessageToConsultingAgentAsync(
        Guid chatId,
        Guid userId,
        MessageDto dto,
        Func<string, Task> statusHandler,
        Guid? manualId,
        string? timeZoneId,
        CancellationToken ct =  default(CancellationToken)
        )
    {
        var chat = await TryCheck(chatId, userId);

        if (chat.Kind != ChatKind.Staff && manualId is null)
        {
            throw new InvalidRequestException("Выберите документ — по нему агент отвечает и сверяет заявку.");
        }

        if (manualId is { } selected) await EnsureManualAllowedAsync(selected, chat.Kind);

        var sessionId = chatId.ToString();

        var prior = (await chatRepository.GetMessagesAsync(chatId, limit: 15))
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .ToList();

        await chatRepository.AddMessageAsync(chatId, dto.MapToMassage());

        var anonymizedHistory = await anonymizeClient.AnonymizeBatchAsync(
            prior.Select(m => m.Content).ToList(), sessionId, ct);

        var history = prior.Select((m, index) => new MessageHistoricalDto
        {
            Content = anonymizedHistory[index],
            SenderRole = m.SenderId == Guid.Empty ? UserRole.Agent : UserRole.User
        });

        var anonymizedPrompt = await anonymizeClient.AnonymizeAsync(dto.Content, sessionId, ct);

        ConsultingAgent agent = agentFactory.CreateConsultingAgent(
            statusHandler, new AgentSession(manualId, chatId, userId, chat.Kind, timeZoneId));

        var anonymizedResponse = await agent.InvokeAsync(anonymizedPrompt, history, ct);
        var responseText = await anonymizeClient.DeanonymizeAsync(anonymizedResponse, sessionId, ct);

        var aiMessage = new MessageDto(string.Empty, responseText, Guid.Empty, DateTime.Now);
        var aiRedisId = await chatRepository.AddMessageAsync(chatId, aiMessage.MapToMassage());

        return new MessageDisplayDto(
            Id: aiRedisId,
            Content: responseText,
            UserRole: UserRole.Agent,
            userName: "ConsultingAgent"
        );
    }

    public async Task<List<ChatDto>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId, ChatKind kind)
    {
        if (kind == ChatKind.Staff) await DenyNotStaffAsync(userId);

        return (await chatRepository.GetUserChatsAsync(userId, Page(limit), lastChatId, kind))
            .Select(c => c.MapToChatDto())
            .ToList();
    }

    private static int Page(int limit) => limit <= 0 ? DefaultPageSize : Math.Min(limit, MaxPageSize);

    private static bool IsPdf(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private (IReadOnlyList<ApplicationSectionDto> Sections, IReadOnlyList<string> Lines) ReadDocxApplication(
        byte[] anonymizedDocx)
    {
        using var stream = new MemoryStream(anonymizedDocx, writable: false);

        var sections = docxTextExtractor.ExtractSections(stream);

        stream.Position = 0;

        return (sections, docxTextExtractor.ExtractLines(stream));
    }

    private async Task<(IReadOnlyList<ApplicationSectionDto> Sections, IReadOnlyList<string> Lines)>
        ReadPdfApplicationAsync(Stream stored, string sessionId, CancellationToken ct)
    {
        var lines = pdfTextExtractor.ExtractLines(stored);

        if (lines.Count == 0) return ([], []);

        var anonymized = await anonymizeClient.AnonymizeBatchAsync(lines, sessionId, ct);

        return (PlainTextSections.Parse(anonymized), anonymized);
    }

    private async Task<Manual> EnsureManualAllowedAsync(Guid manualId, ChatKind kind)
    {
        var manual = await manualRepository.GetManualAsync(manualId)
                     ?? throw new NotFoundException("Документ не найден — отвечать и сверять заявку не по чему.");

        if (manual.Scope == ManualScope.Staff && kind != ChatKind.Staff)
        {
            throw new NotFoundException("Документ не найден — отвечать и сверять заявку не по чему.");
        }

        return manual;
    }
    

    public async Task<QueuedApplicationDto> UploadGrantApplicationAsync(
        Guid chatId,
        Guid userId,
        Guid manualId,
        Stream fileStream,
        string fileName,
        string? content,
        ContestKind contestKind,
        CancellationToken ct)
    {
        var chat = await TryCheck(chatId, userId);

        await EnsureManualAllowedAsync(manualId, chat.Kind);

        var sessionId = chatId.ToString();

        using var originalStream = new MemoryStream();
        await fileStream.CopyToAsync(originalStream, ct);
        var original = originalStream.ToArray();

        if (original.Length == 0)
        {
            throw new InvalidDocumentException("Файл пустой.");
        }

        var applicationId = Guid.NewGuid();
        var storageKey = StorageKeys.GrantApplication(chatId, applicationId, fileName);

        await fileStorage.UploadAsync(storageKey, original, ct);

        var documentId = await documentRegistry.RecordAsync(
            storageKey,
            StoredDocumentKind.GrantApplication,
            userId,
            chatId,
            fileName,
            original.Length,
            ct);

        var userMessageContent = content?.Trim() ?? string.Empty;

        var messageId = await chatRepository.AddMessageAsync(
            chatId,
            new MessageDto(
                    string.Empty,
                    userMessageContent,
                    userId,
                    DateTime.Now,
                    documentId,
                    fileName)
                .MapToMassage());

        await reviewQueue.EnqueueAsync(
            new ReviewJob(
                chatId, userId, manualId, applicationId, documentId, storageKey, fileName, content, contestKind),
            ct);

        await reviewProgress.QueuedAsync(userId, chatId, documentId, fileName);

        var queueDepth = await reviewQueue.PendingAsync();

        logger.LogInformation(
            "Заявка {FileName} принята в чат {ChatId}, поставлена в очередь на разбор", fileName, chatId);

        return new QueuedApplicationDto(messageId, applicationId, documentId, fileName, queueDepth);
    }

    public async Task<MessageDisplayDto> RunApplicationReviewAsync(
        ReviewJob job,
        Func<string, Task> statusHandler,
        CancellationToken ct)
    {
        var sessionId = job.ChatId.ToString();

        if (await chatRepository.GetChatAsync(job.ChatId) is null)
        {
            throw new NotFoundException("Чат удалён — разбирать заявку больше некуда.");
        }

        await statusHandler("Забираю заявку из хранилища...");

        await using var stored = await fileStorage.DownloadAsync(job.StorageKey, ct);

        await statusHandler("Убираю персональные данные перед отправкой в модель...");

        var (sections, documentLines) = IsPdf(job.FileName)
            ? await ReadPdfApplicationAsync(stored, sessionId, ct)
            : ReadDocxApplication(await docxAnonymizer.AnonymizeAsync(stored, sessionId, ct));

        if (sections.Count == 0)
        {
            throw new InvalidDocumentException(
                "Не удалось извлечь текст из документа — он пустой, состоит из картинок "
                + "или это скан без текстового слоя.");
        }

        var anonymizedComment = string.IsNullOrWhiteSpace(job.Comment)
            ? null
            : await anonymizeClient.AnonymizeAsync(job.Comment, sessionId, ct);

        var agent = agentFactory.CreateApplicationReviewAgent(
            statusHandler,
            new AgentSession(job.ManualId, job.ChatId, job.UserId));

        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var balanceBefore = await balanceProbe.SnapshotAsync(ct);

        var anonymizedVerdict = await agent.ReviewAsync(
            sections, documentLines, job.ContestKind, anonymizedComment, ct);

        watch.Stop();
        await RecordReviewSpendAsync(job, startedAt, watch.Elapsed, balanceBefore, ct);

        var verdict = await KeepResultAsync(
            () => anonymizeClient.DeanonymizeAsync(anonymizedVerdict, sessionId, ct), ct);

        var agentMessage = new MessageDto(string.Empty, verdict, Guid.Empty, DateTime.Now);

        var agentRedisId = await KeepResultAsync(
            () => chatRepository.AddMessageAsync(job.ChatId, agentMessage.MapToMassage()), ct);

        if (agent.LastReview is { } review)
        {
            await reviewRepository.SaveAsync(
                job.ChatId,
                job.UserId,
                job.ManualId,
                job.DocumentId,
                agentRedisId,
                job.FileName,
                review,
                ct);
        }

        return new MessageDisplayDto(
            Id: agentRedisId,
            Content: verdict,
            UserRole: UserRole.Agent,
            userName: "ApplicationReviewAgent"
        );
    }

    public async Task<MessageDisplayDto> ReportReviewFailureAsync(ReviewJob job, string reason, CancellationToken ct)
    {
        var content = $"Не получилось разобрать заявку «{job.FileName}». {reason}";

        var messageId = await chatRepository.AddMessageAsync(
            job.ChatId,
            new MessageDto(string.Empty, content, Guid.Empty, DateTime.Now).MapToMassage());

        return new MessageDisplayDto(
            Id: messageId,
            Content: content,
            UserRole: UserRole.Agent,
            userName: "ApplicationReviewAgent"
        );
    }

    public async Task DeleteChatAsync(Guid chatId, Guid userId, CancellationToken ct = default)
    {
        await TryCheck(chatId, userId);

        foreach (var key in await documentRegistry.ListKeysByChatAsync(chatId, ct))
        {
            try
            {
                await fileStorage.DeleteAsync(key, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Файл {StorageKey} удалённого чата остался в хранилище", key);
            }
        }

        await documentRegistry.RemoveByChatAsync(chatId, ct);

        await reviewRepository.RemoveByChatAsync(chatId, ct);

        var removedPins = await pinRepository.DeletePinsAsync(chatId);

        if (!await chatRepository.DeleteChatAsync(chatId))
        {
            throw new NotFoundException("Чат не найден.");
        }

        logger.LogInformation("Чат {ChatId} удалён, заметок вычищено: {Pins}", chatId, removedPins);
    }

    public async Task<DocumentFileDto> ExportReviewAsync(Guid chatId, Guid userId, string messageId)
    {
        await TryCheck(chatId, userId);

        var message = await chatRepository.GetMessageAsync(chatId, messageId)
                      ?? throw new NotFoundException("Сообщение не найдено — возможно, чат истёк.");

        if (message.SenderId != Guid.Empty)
        {
            throw new InvalidRequestException("Выгружать можно только разбор, подготовленный агентом.");
        }

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            throw new InvalidRequestException("В этом сообщении нет текста разбора.");
        }

        var document = new DocumentModel(
            Title: "Разбор заявки",
            Subtitle: $"Автоматическая проверка по методичке, {message.CreateAt:dd.MM.yyyy}",
            Blocks: MarkdownDocumentParser.Parse(message.Content),
            Signature: null,
            Header: null);

        var fileName = $"razbor-zayavki-{message.CreateAt:yyyy-MM-dd}.docx";

        return new DocumentFileDto(docxWriter.Write(document), fileName);
    }
}