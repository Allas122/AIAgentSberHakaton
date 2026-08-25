using Domain.ValueTypes;
﻿using ChatNode.Application.DTO;
using ChatNode.Infrastructure.Review;
using Domain.Entities;

namespace ChatNode.Application.Services.Abstractons;

public interface IChatService
{
    public Task<Guid> CreateChatAsync(string title, Guid userId, ChatKind kind = ChatKind.Grant);
    public Task<List<MessageDisplayDto>> GetMessagesAsync(Guid chatId, Guid userId, int limit, string? lastMessageId);
    public Task<MessageDisplayDto> SendMessageToConsultingAgentAsync(
        Guid chatId,
        Guid userId,
        MessageDto dto,
        Func<string, Task> statusHandler,
        Guid? manualId,
        string? timeZoneId,
        CancellationToken ct
    );
    public Task<List<ChatDto>> GetUserChatsAsync(Guid userId, int limit, Guid? lastChatId, ChatKind kind);

    public Task<QueuedApplicationDto> UploadGrantApplicationAsync(
        Guid chatId,
        Guid userId,
        Guid manualId,
        Stream fileStream,
        string fileName,
        string? content,
        ContestKind contestKind,
        CancellationToken ct
    );

    public Task<MessageDisplayDto> RunApplicationReviewAsync(
        ReviewJob job,
        Func<string, Task> statusHandler,
        CancellationToken ct
    );

    public Task<MessageDisplayDto> ReportReviewFailureAsync(
        ReviewJob job,
        string reason,
        CancellationToken ct
    );

    public Task<IReadOnlyList<ActiveReviewDto>> GetActiveReviewsAsync(Guid userId);

    public Task<DocumentFileDto> ExportReviewAsync(Guid chatId, Guid userId, string messageId);

    public Task DeleteChatAsync(Guid chatId, Guid userId, CancellationToken ct = default);
}