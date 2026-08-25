namespace ChatNode.Infrastructure.Review;

public interface IReviewNotifier
{
    Task StatusAsync(Guid userId, Guid chatId, string status);

    Task FileStatusAsync(Guid userId, string fileName, string status);

    Task ReviewReadyAsync(Guid userId, Guid chatId, Guid? documentId, string messageId, string content);

    Task ReviewFailedAsync(Guid userId, Guid chatId, string fileName, string messageId, string reason);
}
