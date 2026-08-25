using System.Diagnostics;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Metering;

public static class TokenMeterExtensions
{
    public static async Task<ChatCompletion> MeasureChatAsync(
        this ITokenMeter meter,
        TokenOperation operation,
        string model,
        Func<Task<ChatCompletion>> call,
        Guid? chatId = null,
        Guid? userId = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();

        try
        {
            var completion = await call();
            watch.Stop();

            await meter.RecordAsync(new TokenRecord(
                operation,
                Name(completion.Model, model),
                startedAt,
                watch.Elapsed,
                PromptTokens: completion.Usage?.PromptTokens ?? 0,
                CompletionTokens: completion.Usage?.CompletionTokens ?? 0,
                TotalTokens: completion.Usage?.TotalTokens ?? 0,
                ToolCalls: 1,
                ChatId: chatId,
                UserId: userId));

            return completion;
        }
        catch (Exception)
        {
            watch.Stop();
            await Fail(meter, operation, model, startedAt, watch.Elapsed, chatId, userId);
            throw;
        }
    }

    public static async Task<FunctionChatResult> MeasureToolsAsync(
        this ITokenMeter meter,
        TokenOperation operation,
        string model,
        Func<Task<FunctionChatResult>> call,
        Guid? chatId = null,
        Guid? userId = null)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();

        try
        {
            var result = await call();
            watch.Stop();

            var usage = result.Completion.Usage;

            await meter.RecordAsync(new TokenRecord(
                operation,
                Name(result.Completion.Model, model),
                startedAt,
                watch.Elapsed,
                PromptTokens: usage?.PromptTokens ?? 0,
                CompletionTokens: usage?.CompletionTokens ?? 0,
                TotalTokens: usage?.TotalTokens ?? 0,
                ToolCalls: result.FunctionCalls.Count + 1,
                Partial: true,
                ChatId: chatId,
                UserId: userId));

            return result;
        }
        catch (Exception)
        {
            watch.Stop();
            await Fail(meter, operation, model, startedAt, watch.Elapsed, chatId, userId, partial: true);
            throw;
        }
    }

    public static async Task<Embeddings> MeasureEmbeddingsAsync(
        this ITokenMeter meter,
        string model,
        Func<Task<Embeddings>> call)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();

        try
        {
            var embeddings = await call();
            watch.Stop();

            var prompt = embeddings.Data.Sum(item => item.Usage?.PromptTokens ?? 0);

            await meter.RecordAsync(new TokenRecord(
                TokenOperation.Embedding,
                model,
                startedAt,
                watch.Elapsed,
                PromptTokens: prompt,
                TotalTokens: prompt,
                ToolCalls: embeddings.Data.Count));

            return embeddings;
        }
        catch (Exception)
        {
            watch.Stop();
            await Fail(meter, TokenOperation.Embedding, model, startedAt, watch.Elapsed, null, null);
            throw;
        }
    }

    private static Task Fail(
        ITokenMeter meter,
        TokenOperation operation,
        string model,
        DateTimeOffset startedAt,
        TimeSpan duration,
        Guid? chatId,
        Guid? userId,
        bool partial = false) =>
        meter.RecordAsync(
            new TokenRecord(
                operation,
                model,
                startedAt,
                duration,
                Partial: partial,
                Failed: true,
                ChatId: chatId,
                UserId: userId),
            CancellationToken.None);

    private static string Name(string? reported, string requested) =>
        string.IsNullOrWhiteSpace(reported) ? requested : reported;
}
