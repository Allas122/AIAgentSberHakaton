using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Entities;
using Domain.Repositories;
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
    Guid manualId
) : IAgent
{
    private readonly GigaChatOptions _gigaChatOptions = gigaChatOptions.Value;
 
    private const int TargetChunkSize = 4000;
 
    private const int MaxBoundarySearchWindow = 1500;
 
    private const int OverlapSize = 500;
    private const int MaxToolCallsPerChunk = 40;

    private const string ToolCallLimitMarker = "exceeded the maximum of";

    public async Task<string> InvokeAsync(string fullPrompt, CancellationToken ct)
    {
        List<string> unresolvedChunks = new();

        try
        {
            var chunks = SplitIntoStructuralChunks(fullPrompt, TargetChunkSize, MaxBoundarySearchWindow);

            string lastOverlap = "";

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

                bool exhausted = await TryRunAsync(promptWithContext, $"чанк {i + 1}/{chunks.Count}", ct);

                if (exhausted)
                {
                    unresolvedChunks.Add(currentNewText);
                }

                lastOverlap = currentNewText.Length > OverlapSize
                    ? currentNewText[^OverlapSize..]
                    : currentNewText;
            }

            if (unresolvedChunks.Count > 0)
            {
                return await RetryUnresolvedChunksAsync(unresolvedChunks, manualId, ct);
            }

            return "Мануал успешно обработан и сохранен в базу знаний!";
        }
        catch (OperationCanceledException)
        {
            await manualRepository.DeleteManualAsync(manualId);
            return "Операция была прервана пользователем. Данные удалены.";
        }
    }
    
    private async Task<bool> TryRunAsync(string prompt, string label, CancellationToken ct)
    {
        try
        {
            await RunAsync(prompt, manualId, ct);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsToolCallLimit(ex))
        {
            Console.WriteLine($"[PARSE BUDGET]: {label} -> лимит вызовов исчерпан, чанк уйдёт на повтор половинками");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PARSE FAILED]: {label} -> {GigaChatRetry.Describe(ex)}");
            return true;
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
        ManualFunctionsToolsSet manualFunctionsToolsSet = new ManualFunctionsToolsSet(manualRepository, manualId);
 
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
 
        await gigaChatClient.ChatWithToolsAsync(
            chat,
            manualFunctionsToolsSet.FunctionTools,
            maxToolCalls: MaxToolCallsPerChunk,
            cancellationToken: ct);
    }
    
    private async Task<string> RetryUnresolvedChunksAsync(List<string> unresolvedChunks, Guid manualId, CancellationToken ct)
    {
        var stillUnresolved = new List<string>();

        Console.WriteLine($"[PARSE RETRY]: повторяю {unresolvedChunks.Count} чанк(ов) через {_gigaChatOptions.OperationRetryDelaySeconds:0.#} с");
        await Task.Delay(TimeSpan.FromSeconds(_gigaChatOptions.OperationRetryDelaySeconds), ct);

        foreach (var chunk in unresolvedChunks)
        {
            var subChunks = SplitIntoStructuralChunks(chunk, TargetChunkSize / 2, MaxBoundarySearchWindow / 2);

            foreach (var sub in subChunks)
            {
                ct.ThrowIfCancellationRequested();
                bool exhausted = await TryRunAsync(sub, "повторный фрагмент", ct);
                if (exhausted)
                {
                    stillUnresolved.Add(sub);
                }
            }
        }

        if (stillUnresolved.Count > 0)
        {
            return $"Мануал обработан, но {stillUnresolved.Count} фрагмент(ов) не удалось " +
                   "гарантированно закрыть даже после повторной попытки — рекомендуется ручная проверка.";
        }

        return "Мануал успешно обработан и сохранен в базу знаний!";
    }

    private static bool IsToolCallLimit(Exception ex) =>
        ex.Message.Contains(ToolCallLimitMarker, StringComparison.OrdinalIgnoreCase);
}