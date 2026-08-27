using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public record ArgumentConsole(string Message);

public class ManualParserAgent(
    IGigaChatClient gigaChatClient,
    IManualRepository manualRepository,
    IOptions<GigaChatOptions> gigaChatOptions,
    ITokenMeter tokenMeter,
    ILoggerFactory loggerFactory,
    Guid manualId
)
{
    private readonly GigaChatOptions _gigaChatOptions = gigaChatOptions.Value;

    private readonly ILogger<ManualParserAgent> _logger = loggerFactory.CreateLogger<ManualParserAgent>();

    private const int TargetChunkSize = 4000;

    private const int MaxBoundarySearchWindow = 1500;

    private const int OverlapSize = 500;
    private const int MaxToolCallsPerChunk = 40;

    private const int PreviewLength = 90;

    private static readonly string[] ParseFunctions =
    [
        "add_manual_part",
        "add_manual_navigation_header",
        "get_manual_full_navigation"
    ];

    private const string ToolCallLimitMarker = "exceeded the maximum of";

    private const string BudgetReason = "модель исчерпала лимит вызовов инструментов на фрагмент";

    private record FailedChunk(string Text, string Reason);

    public async Task<ManualParseReport> ParseAsync(
        string fullText,
        Func<ManualParseProgress, Task>? onProgress,
        CancellationToken ct)
    {
        var chunks = SplitIntoStructuralChunks(fullText, TargetChunkSize, MaxBoundarySearchWindow);
        var failed = new List<FailedChunk>();

        int processed = 0;
        string lastOverlap = "";

        await ReportAsync(onProgress, chunks.Count, processed, failed.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string currentNewText = chunks[i];

            string promptWithContext = $"""
                                        ### КОНТЕКСТ (уже обработано, НЕ добавлять повторно):
                                        ...{lastOverlap}

                                        ### НОВЫЙ ТЕКСТ ДЛЯ СЕГМЕНТАЦИИ (чанк {i + 1}/{chunks.Count}):
                                        {currentNewText}
                                        """;

            string? reason = await TryRunAsync(promptWithContext, $"чанк {i + 1}/{chunks.Count}", ct);

            if (reason is null) processed++;
            else failed.Add(new FailedChunk(currentNewText, reason));

            lastOverlap = currentNewText.Length > OverlapSize
                ? currentNewText[^OverlapSize..]
                : currentNewText;

            await ReportAsync(onProgress, chunks.Count, processed, failed.Count);
        }

        if (failed.Count == 0)
        {
            return new ManualParseReport(chunks.Count, processed, []);
        }

        return await RetryFailedChunksAsync(chunks.Count, processed, failed, onProgress, ct);
    }

    private async Task<string?> TryRunAsync(string prompt, string label, CancellationToken ct)
    {
        try
        {
            await RunAsync(prompt, manualId, ct);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsToolCallLimit(ex))
        {
            _logger.LogWarning(
                ex,
                "Разбор методички {ManualId}: {Chunk} -> лимит вызовов исчерпан, чанк уйдёт на повтор половинками",
                manualId,
                label);

            return BudgetReason;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Разбор методички {ManualId}: {Chunk} не обработан -> {Failure}",
                manualId,
                label,
                GigaChatRetry.Describe(ex));

            return GigaChatRetry.Describe(ex);
        }
    }

    private static List<string> SplitIntoStructuralChunks(string text, int targetSize, int searchWindow)
    {
        var chunks = new List<string>();
        int offset = 0;
 
        while (offset < text.Length)
        {
            int remaining = text.Length - offset;
 
            if (remaining <= targetSize)
            {
                chunks.Add(text.Substring(offset));
                break;
            }
 
            int idealCut = offset + targetSize;
            int searchStart = Math.Max(offset + 1, idealCut - searchWindow);
 
            int cut = FindBestBoundary(text, searchStart, idealCut);
 
            if (cut <= offset)
            {
                cut = idealCut;
            }
 
            chunks.Add(text.Substring(offset, cut - offset));
            offset = cut;
        }
 
        return chunks;
    }
 
    private static int FindBestBoundary(string text, int searchStart, int idealCut)
    {
        var candidates = new (string pattern, int priority)[]
        {
            ("\n#", 0),
            ("\n\n", 1),
            ("\n\n*   ", 1),
        };
 
        int bestPos = -1;
        int bestPriority = int.MaxValue;
 
        foreach (var (pattern, priority) in candidates)
        {
            int pos = text.LastIndexOf(pattern, Math.Min(idealCut, text.Length - 1), idealCut - searchStart);
            if (pos > searchStart && priority < bestPriority)
            {
                bestPriority = priority;
                bestPos = pos;
            }
        }
 
        if (bestPos > 0) return bestPos;
 
        foreach (var end in new[] { ". ", "! ", "? " })
        {
            int pos = text.LastIndexOf(end, Math.Min(idealCut, text.Length - 1), idealCut - searchStart);
            if (pos > searchStart)
            {
                return pos + end.Length;
            }
        }
 
        return -1;
    }
    
    private async Task RunAsync(string prompt, Guid manualId, CancellationToken ct)
    {
        ManualFunctionsToolsSet manualFunctionsToolsSet = new ManualFunctionsToolsSet(
            manualRepository,
            manualId,
            loggerFactory.CreateLogger<ManualFunctionsToolsSet>());
 
        Chat chat = new Chat()
        {
            Messages =
            [
                Messages.System(@"
Ты — агент-сегментатор. Твоя работа — наполнять базу знаний(RAG).
Поскольку ты получаешь текст частями, используй функции навигации как свою ""длительную память"" уже имеющихся заголовков.
 
Алгоритм твоих действий:
1. Найти логическую часть мануала, добавить её через add_manual_part.
2. Добавить её заголовки через add_manual_navigation_header.
 
Требования к части мануала - Должно быть логически отделено и включать в себя вырезанный текст из мануала.
Игнорировать какие-либо логические части мануала строго запрещено!
Ограничение: Все части мануала в общем(title+content+navigation) должны умещаться в 400 токенов. Если не хватает, дроби
на 2 части мануала, так чтобы без прерываний и целостность предложений сохранилась. Чем меньше текста
ты засунешь, тем лучше.
Требование к навигации в мануале: Должна содержать список всех Navigation свойств из Navigation частей мануала.
 
Текст в блоке ""КОНТЕКСТ (уже обработано, НЕ добавлять повторно)"" — это хвост предыдущего чанка,
он нужен тебе только для понимания, где ты остановился. Повторный вызов add_manual_part для него запрещён.
 
Обработка ответов инструментов:
- Если add_manual_part вернул status ""error"" с сообщением про размер (""слишком большая"") —
  это значит твоя часть превысила лимит. Раздели именно эту логическую часть на 2 более мелкие
  по естественной смысловой границе (например, по подпунктам) и вызови add_manual_part для каждой
  отдельно. Не пропускай текст из-за этой ошибки — раздел всё равно обязателен к добавлению.
- Если add_manual_navigation_header вернул status ""Skipped"" — значит такой заголовок (или очень
  похожий) уже есть в навигации. Это нормально, не пытайся переформулировать его и добавить ещё раз.
"),
                Messages.User(prompt)
            ],
            FunctionCall = FunctionCallMode.Auto,
            Model = _gigaChatOptions.ManualParserAgentModel,
        };
 
        await tokenMeter.MeasureToolsAsync(
            TokenOperation.ManualParse,
            chat.Model,
            () => gigaChatClient.ChatWithToolsAsync(
                chat,
                manualFunctionsToolsSet.FunctionTools
                    .Where(tool => ParseFunctions.Contains(tool.Name))
                    .ToList(),
                maxToolCalls: MaxToolCallsPerChunk,
                cancellationToken: ct));
    }
    
    private async Task<ManualParseReport> RetryFailedChunksAsync(
        int totalChunks,
        int processed,
        List<FailedChunk> failed,
        Func<ManualParseProgress, Task>? onProgress,
        CancellationToken ct)
    {
        var gaps = new List<ManualParseGap>();

        _logger.LogInformation(
            "Разбор методички {ManualId}: повторяю {Failed} чанк(ов) через {Delay:0.#} с",
            manualId,
            failed.Count,
            _gigaChatOptions.OperationRetryDelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(_gigaChatOptions.OperationRetryDelaySeconds), ct);

        foreach (var chunk in failed)
        {
            var subChunks = SplitIntoStructuralChunks(chunk.Text, TargetChunkSize / 2, MaxBoundarySearchWindow / 2);

            bool recovered = true;

            foreach (var sub in subChunks)
            {
                ct.ThrowIfCancellationRequested();

                string? reason = await TryRunAsync(sub, "повторный фрагмент", ct);
                if (reason is null) continue;

                recovered = false;
                gaps.Add(new ManualParseGap(Preview(sub), reason));
            }

            if (recovered) processed++;

            await ReportAsync(onProgress, totalChunks, processed, totalChunks - processed);
        }

        return new ManualParseReport(totalChunks, processed, gaps);
    }

    private static Task ReportAsync(
        Func<ManualParseProgress, Task>? onProgress,
        int total,
        int processed,
        int failed) =>
        onProgress?.Invoke(new ManualParseProgress(total, processed, failed)) ?? Task.CompletedTask;

    private static string Preview(string fragment)
    {
        var line = fragment
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('#', '*', ' '))
            .FirstOrDefault(x => x.Length > 0) ?? fragment.Trim();

        return line.Length <= PreviewLength ? line : $"{line[..PreviewLength]}…";
    }

    private static bool IsToolCallLimit(Exception ex) =>
        ex.Message.Contains(ToolCallLimitMarker, StringComparison.OrdinalIgnoreCase);
}