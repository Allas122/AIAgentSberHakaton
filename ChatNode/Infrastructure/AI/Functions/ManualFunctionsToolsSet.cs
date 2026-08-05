using System.Text.Json;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Mappers;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class ManualFunctionsToolsSet(IManualRepository repository, Guid manualId) : IFunctionToolsSet
{
    private const int MaxContentTokensApprox = 400;
    private const double ApproxCharsPerToken = 4.0;
    private const int MaxSearchResults = 3;
    private const int MaxViewContentChars = 700;

    private readonly RepeatCallGuard _guard = new();
    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<AddManualPartArguments>(
            name: "add_manual_part",
            description: "Добавляет часть мануала в хранилище, никак не влияет на сам мануал.",
            handler: AddManualPartHandler,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["title"] = FunctionParameter.String("Титул части мануала."),
                    ["content"] = FunctionParameter.String(
                        @"Прямая цитата из мануала, содержащая правила и основной смысл этой части мануала, исключая заголовки."),
                    ["navigation"] = FunctionParameter.String(
                        @"Это строка, которая описывает под какими заголовками находится эта часть мануала. 
                                 Должна включать основные заголовки этой части мануала через разделитель >."
                        )
                },
                required: ["title", "content", "navigation"]
                )
        ),
        FunctionTool.Create<NullableArguments>(
            name: "get_manual_full_navigation",
            description: @"Возвращает текущий список всех добавленных или изменённых разделов мануала через запятые.",
            handler: GetManualFullNavigation
        ),
        FunctionTool.Create<AddManualFullNavigationPartArgument>(
            name: "add_manual_navigation_header",
            description: @"Добавляет в текущее состояние навигации по мануалу новую навигационную строку.
                            Если такой (или очень похожий) заголовок уже есть, ничего не добавляется.",
            handler: AddManualFullNavigation,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["navigation"] = FunctionParameter.String(
                        @"Заголовок раздела мануала, который ты хочешь добавить."
                    )
                },
                required: ["navigation"]
            )
        ),
        FunctionTool.Create<SearchArguments>(
            name: "search_in_manual",
            description: "Позволяет по query найти в RAG часть мануала.",
            handler: SearchInManualHandler,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["query"] = FunctionParameter.String(
                            "Строка, по которой нужно что-то найти в мануале."
                    ),
                    ["limit"] = FunctionParameter.Integer(
                        $"Сколько частей методички вернуть, от 1 до {MaxSearchResults}."
                        )
                },
                required: ["query", "limit"]
            ))
    ];


    public async Task<string> SearchInManualHandler(SearchArguments arguments)
    {
        if (_guard.IsRepeat("search_in_manual", arguments.Query))
        {
            return RepeatCallGuard.RepeatResponse("search_in_manual");
        }

        var limit = Math.Clamp(arguments.Limit, 1, MaxSearchResults);

        var parts = await repository.KnnSearchManualPartAsync(arguments.Query, limit, manualId);

        var views = parts
            .Select(part => new ManualPartView(part.Title, part.Navigation, Trim(part.Content)))
            .ToList();

        Console.WriteLine(
            $"[SEARCHING]:{arguments.Query}:{limit} -> {views.Count}, " +
            $"{views.Sum(view => view.Content.Length)} симв.");

        return ToolJson.Serialize(views);
    }

    public async Task<string> AddManualPartHandler(AddManualPartArguments arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments.Content) || arguments.Content.Length < 10)
        {
            return "{\"status\": \"error\", \"message\": \"Content too short\"}";
        }

        int approxTokens = EstimateTokenCount(arguments.Title, arguments.Content, arguments.Navigation);

        if (approxTokens <= MaxContentTokensApprox)
        {
            var manualPartId = await CreatePartAsync(arguments.Title, arguments.Content, arguments.Navigation);
            return $"{{\"manualPartId\":\"{manualPartId}\"}}";
        }

        int partsNeeded = (int)Math.Ceiling(approxTokens / (double)MaxContentTokensApprox);
        int approxMaxCharsPerPart = (int)(MaxContentTokensApprox * ApproxCharsPerToken * 0.9);

        var pieces = TextSplitter.SplitIntoNParts(arguments.Content, partsNeeded);

        var finalPieces = new List<string>();
        foreach (var piece in pieces)
        {
            if (piece.Length <= approxMaxCharsPerPart)
            {
                finalPieces.Add(piece);
            }
            else
            {
                int subParts = (int)Math.Ceiling((double)piece.Length / approxMaxCharsPerPart);
                finalPieces.AddRange(TextSplitter.SplitIntoNParts(piece, subParts));
            }
        }

        var createdIds = new List<Guid>();
        for (int i = 0; i < finalPieces.Count; i++)
        {
            string pieceTitle = finalPieces.Count > 1
                ? $"{arguments.Title} (Часть {i + 1}/{finalPieces.Count})"
                : arguments.Title;

            var id = await CreatePartAsync(pieceTitle, finalPieces[i], arguments.Navigation);
            createdIds.Add(id);
        }

        Console.WriteLine(
            $"[AUTO-SPLIT]: {arguments.Title} (~{approxTokens} tokens) -> {finalPieces.Count} частей");

        return ToolJson.Serialize(new
        {
            status = "ok_split",
            message = $"Часть была больше лимита (~{approxTokens} токенов) и автоматически разделена " +
                      $"на {finalPieces.Count} частей кодом. Повторно вызывать add_manual_part для этого " +
                      "текста не нужно — весь контент уже сохранён.",
            manualPartIds = createdIds
        });
    }

    private async Task<Guid> CreatePartAsync(string title, string content, string navigation)
    {
        var manualPartDto = new ManualPartDto(
            Guid.NewGuid(),
            manualId,
            title,
            content,
            navigation
        );

        Console.WriteLine($"[INDEXING]: {navigation} -> {title} ({content.Length} chars)");

        return await repository.CreateManualPartAsync(manualPartDto.MapToManualPart());
    }

    public async Task<string> GetManualFullNavigation(NullableArguments s)
    {
        var manualNavigation = (await repository.GetManualAsync(manualId)).Navigation;
        var res = new GetManualFullNavigationReturn(manualNavigation);
        return ToolJson.Serialize(res);
    }

    public async Task<string> AddManualFullNavigation(AddManualFullNavigationPartArgument arguments)
    {
        var manual = (await repository.GetManualAsync(manualId)).MapToManualDto();

        var existingHeaders = SplitNavigation(manual.Navigation);
        string incoming = arguments.Navigation.Trim();

        if (IsDuplicateOrSimilar(existingHeaders, incoming))
        {
            var skippedRes = new SetManualFullNavigationReturn(
                "Skipped",
                "Такой (или очень похожий) заголовок уже есть в навигации, повторно не добавлен.");
            return ToolJson.Serialize(skippedRes);
        }

        existingHeaders.Add(incoming);
        var newManual = new ManualDto(manual.Id, manual.Title, string.Join(", ", existingHeaders));

        await repository.UpdateManualAsync(newManual.MapToManual());

        var res = new SetManualFullNavigationReturn("Ok", "Навигация обновлена");
        return ToolJson.Serialize(res);
    }

    private static List<string> SplitNavigation(string? navigation)
    {
        if (string.IsNullOrWhiteSpace(navigation)) return new List<string>();

        return navigation
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool IsDuplicateOrSimilar(List<string> existing, string incoming)
    {
        string normalizedIncoming = Normalize(incoming);

        foreach (var header in existing)
        {
            string normalizedExisting = Normalize(header);

            if (normalizedExisting == normalizedIncoming)
                return true;

            if (normalizedExisting.Contains(normalizedIncoming) ||
                normalizedIncoming.Contains(normalizedExisting))
                return true;
        }

        return false;
    }

    private static string Trim(string content) =>
        content.Length <= MaxViewContentChars ? content : content[..MaxViewContentChars] + "...";

    private static string Normalize(string s) =>
        s.Trim().ToLowerInvariant();

    private static int EstimateTokenCount(params string[] parts)
    {
        int totalChars = parts.Sum(p => p?.Length ?? 0);
        return (int)Math.Ceiling(totalChars / ApproxCharsPerToken);
    }
}
public static class TextSplitter
{
    public static List<string> SplitIntoStructuralChunks(string text, int targetSize, int searchWindow)
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
    
    public static List<string> SplitIntoNParts(string text, int parts)
    {
        if (parts <= 1) return [text];
 
        int approxPartSize = (int)Math.Ceiling((double)text.Length / parts);
        int searchWindow = Math.Max(50, approxPartSize / 4);
 
        return SplitIntoStructuralChunks(text, approxPartSize, searchWindow);
    }
 
    private static int FindBestBoundary(string text, int searchStart, int idealCut)
    {
        var candidates = new (string pattern, int priority)[]
        {
            ("\n#", 0),
            ("\n\n", 1),
            ("\n\n*   ", 1),
            ("\n*   ", 1),
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
 
        foreach (var end in new[] { ". ", "! ", "? ", "\n" })
        {
            int pos = text.LastIndexOf(end, Math.Min(idealCut, text.Length - 1), idealCut - searchStart);
            if (pos > searchStart)
            {
                return pos + end.Length;
            }
        }
 
        return -1;
    }
}