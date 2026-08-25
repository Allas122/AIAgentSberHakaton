using Domain.Entities;
using Domain.Repositories;

namespace ChatNode.Infrastructure.AI.Metering;

public class TokenMeter(ITokenUsageRepository repository, ILogger<TokenMeter> logger) : ITokenMeter
{
    public async Task RecordAsync(TokenRecord record, CancellationToken ct = default)
    {
        logger.LogDebug(
            "Токены: операция={Operation} chatId={ChatId} model={Model} prompt={PromptTokens} " +
            "completion={CompletionTokens} total={TotalTokens} частично={Partial} вызовов={ToolCalls} " +
            "за {Duration:0.0} с, ошибка={Failed}",
            record.Operation,
            record.ChatId,
            record.Model,
            record.PromptTokens,
            record.CompletionTokens,
            record.TotalTokens,
            record.Partial,
            record.ToolCalls,
            record.Duration.TotalSeconds,
            record.Failed);

        try
        {
            await repository.AddAsync(
                new TokenUsage
                {
                    Id = Guid.NewGuid(),
                    Operation = record.Operation,
                    Model = record.Model,
                    PromptTokens = record.PromptTokens,
                    CompletionTokens = record.CompletionTokens,
                    TotalTokens = record.TotalTokens,
                    ToolCalls = record.ToolCalls,
                    Partial = record.Partial,
                    Failed = record.Failed,
                    BalanceSpent = record.BalanceSpent,
                    ChatId = record.ChatId,
                    UserId = record.UserId,
                    StartedAt = record.StartedAt,
                    Duration = record.Duration
                },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось записать расход токенов по операции {Operation}", record.Operation);
        }
    }
}
