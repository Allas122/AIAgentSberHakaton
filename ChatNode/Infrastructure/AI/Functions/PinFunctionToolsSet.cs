using System.Text.Json;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class PinFunctionToolsSet(
    IPinRepository repository,
    Guid sessionId,
    ILogger logger,
    double duplicateDistance = 0.15,
    double searchDistance = 0.55,
    FindingScope scope = FindingScope.Fragment) : IFunctionToolsSet
{
    private readonly RepeatCallGuard _guard = new();
    private readonly List<PinJournalEntry> _journal = [];

    private const int DefaultSearchLimit = 3;
    private const int MaxSearchLimit = 5;
    private const int MaxGetPinsLimit = 10;
    private const int MaxPinContentChars = 300;

    public IReadOnlyList<PinJournalEntry> Journal => _journal;

    private const string PinTypeDescription =
        "Категория: Mistake — нарушено требование методички, видно прямо в этом фрагменте; " +
        "Attention — требование заявлено, но не подтверждено: нет цифры, срока, расчёта или механизма; " +
        "WhatToCheck — нужно сверить с другими разделами заявки; " +
        "Summary — о чём этот фрагмент, одна-две строки.";

    private static readonly string[] PinTypeValues = ["WhatToCheck", "Summary", "Mistake", "Attention"];

    private static readonly Regex ServiceTokens = new(@"<\|[^|>]*\|>", RegexOptions.Compiled);

    private static readonly Regex AbsenceWording = new(
        @"(не\s+указан\w*|не\s+приведен\w*|не\s+представлен\w*|не\s+описан\w*|не\s+прописан\w*|" +
        @"не\s+содержит\w*|не\s+раскрыт\w*|не\s+определ[её]н\w*|отсутству\w*|нет\s+информации|" +
        @"не\s+хватает|не\s+упомян\w*|не\s+заполнен\w*|не\s+сформулирован\w*|не\s+обоснован\w*|" +
        @"не\s+найден\w*|не\s+обнаружен\w*|не\s+предусмотрен\w*|не\s+подтвержд\w*|" +
        @"нет\s+раздела|нет\s+данных|нигде\s+не\s+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnonymizationTag = new(@"\[[A-Z][A-Z_]*_[0-9A-F]+\]", RegexOptions.Compiled);

    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<CreatePinArguments>(
            name: "create_pin",
            description: "Записать находку по этому фрагменту заявки. " +
                         "Проверять заранее, нет ли такой заметки, не нужно — дубликаты отсекаются сами.",
            handler: CreatePin,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["content"] = FunctionParameter.String("Что найдено и какое требование методички затронуто."),
                    ["pin_type"] = FunctionParameter.String(PinTypeDescription, PinTypeValues),
                    ["criterion"] = FunctionParameter.Integer(
                        "Номер критерия оценки, к которому относится находка, из списка КРИТЕРИИ. " +
                        "Если находка не ложится ни на один критерий — 0.")
                },
                required: ["content", "pin_type", "criterion"]
            )
        ),
        FunctionTool.Create<UpdatePinArguments>(
            name: "update_pin",
            description: "Изменить существующую заметку: уточнить текст и, если нужно, сменить категорию.",
            handler: UpdatePin,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["pin_id"] = FunctionParameter.String("ID заметки, полученный от create_pin."),
                    ["content"] = FunctionParameter.String("Новое содержание заметки целиком."),
                    ["pin_type"] = FunctionParameter.String(
                        "Новая категория, если её нужно изменить. " + PinTypeDescription, PinTypeValues)
                },
                required: ["pin_id", "content"]
            )
        ),
        FunctionTool.Create<DeletePinArguments>(
            name: "delete_pin",
            description: "Удалить заметку, которая оказалась ошибочной или потеряла смысл. " +
                         "Наблюдение при этом остаётся в накопительной сводке по заявке.",
            handler: DeletePin,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["pin_id"] = FunctionParameter.String("ID заметки."),
                    ["reason"] = FunctionParameter.String("Почему заметка удаляется.")
                },
                required: ["pin_id"]
            )
        ),
        FunctionTool.Create<SearchPinsArguments>(
            name: "search_pins",
            description: "Найти ранее сделанные заметки по смыслу.",
            handler: SearchPins,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["query"] = FunctionParameter.String("Тема или ключевые слова."),
                    ["pin_type"] = FunctionParameter.String("Фильтр по категории, необязателен.", PinTypeValues),
                    ["limit"] = FunctionParameter.Integer($"Сколько заметок вернуть, от 1 до {MaxSearchLimit}.")
                },
                required: ["query"]
            )
        ),
        FunctionTool.Create<GetPinsArguments>(
            name: "get_pins",
            description: $"Получить последние заметки по заявке, не больше {MaxGetPinsLimit}. " +
                         "Для поиска по теме лучше подходит search_pins.",
            handler: GetPins,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["pin_type"] = FunctionParameter.String("Фильтр по категории, необязателен.", PinTypeValues)
                }
            )
        )
    ];

    public async Task<string> CreatePin(CreatePinArguments arguments)
    {
        var content = Sanitize(arguments.Content);

        if (string.IsNullOrWhiteSpace(content))
        {
            return Error("Содержание заметки пустое, создавать нечего.");
        }

        if (!TryParsePinType(arguments.PinType, out var requestedType))
        {
            return Error($"Неизвестная категория заметки: \"{arguments.PinType}\". " +
                         "Допустимые значения: WhatToCheck, Summary, Mistake, Attention.");
        }

        if (_guard.IsRepeat("create_pin", arguments.PinType, content))
        {
            return RepeatCallGuard.RepeatResponse("create_pin");
        }

        var pinType = Downgrade(requestedType, content);

        var duplicate = (await repository.KnnSearchPinsAsync(sessionId, content, 1, pinType, duplicateDistance))
            .FirstOrDefault();

        if (duplicate is not null)
        {
            logger.LogDebug(
                "Заметки чата {ChatId}: дубликат {PinType} -> {PinId} (расстояние {Distance:0.###})",
                sessionId,
                pinType,
                duplicate.Pin.Id,
                duplicate.Distance);

            return ToolJson.Serialize(new CreatePinReturn(
                "Duplicate",
                "Такая заметка уже есть, новая не создана. Не переписывай её другими словами. " +
                "Если появилась существенная подробность — дополни существующую через update_pin, " +
                "иначе переходи к следующему наблюдению.",
                duplicate.Pin.Id));
        }

        var criterion = arguments.Criterion > 0 ? arguments.Criterion : (int?)null;

        var pinId = await repository.CreatePinAsync(
            new Pin(Guid.NewGuid(), sessionId, content, pinType, DateTimeOffset.UtcNow, criterion, scope));

        logger.LogDebug("Заметки чата {ChatId}: создана {PinType} -> {PinId}", sessionId, pinType, pinId);

        _journal.Add(new PinJournalEntry(PinAction.Created, pinType, content));

        return ToolJson.Serialize(
            new CreatePinReturn("Ok", DowngradeNote(requestedType, pinType, content), pinId));
    }

    public async Task<string> UpdatePin(UpdatePinArguments arguments)
    {
        if (!Guid.TryParse(arguments.PinId, out var pinId))
        {
            return Error($"\"{arguments.PinId}\" не является идентификатором заметки.");
        }

        var content = Sanitize(arguments.Content);

        if (string.IsNullOrWhiteSpace(content))
        {
            return Error("Нечего обновлять: новое содержание пустое.");
        }

        if (!TryParseOptionalPinType(arguments.PinType, out var requestedType))
        {
            return Error($"Неизвестная категория заметки: \"{arguments.PinType}\". " +
                         "Допустимые значения: WhatToCheck, Summary, Mistake, Attention.");
        }

        if (_guard.IsRepeat("update_pin", arguments.PinId, content, arguments.PinType))
        {
            return RepeatCallGuard.RepeatResponse("update_pin");
        }

        var pin = await repository.GetPinAsync(sessionId, pinId);
        if (pin is null)
        {
            return Error($"Заметка {pinId} не найдена.");
        }

        var storedType = requestedType is null ? pin.Type : Downgrade(requestedType.Value, content);

        var updated = await repository.UpdatePinAsync(pin with { Content = content, Type = storedType });
        if (!updated)
        {
            return Error($"Заметка {pinId} не найдена.");
        }

        logger.LogDebug(
            "Заметки чата {ChatId}: обновлена {PreviousType} -> {PinType} {PinId}",
            sessionId,
            pin.Type,
            storedType,
            pinId);

        _journal.Add(new PinJournalEntry(PinAction.Updated, storedType, content));

        var note = requestedType is null || storedType == requestedType
            ? storedType == pin.Type ? "Заметка обновлена." : $"Заметка обновлена, категория теперь {storedType}."
            : DowngradeNote(requestedType.Value, storedType, content);

        return ToolJson.Serialize(new PinActionReturn("Ok", note));
    }

    public async Task<string> DeletePin(DeletePinArguments arguments)
    {
        if (!Guid.TryParse(arguments.PinId, out var pinId))
        {
            return Error($"\"{arguments.PinId}\" не является идентификатором заметки.");
        }

        if (_guard.IsRepeat("delete_pin", arguments.PinId))
        {
            return RepeatCallGuard.RepeatResponse("delete_pin");
        }

        var pin = await repository.GetPinAsync(sessionId, pinId);
        if (pin is null)
        {
            return Error($"Заметка {pinId} не найдена.");
        }

        if (!await repository.DeletePinAsync(sessionId, pinId))
        {
            return Error($"Заметка {pinId} не найдена.");
        }

        logger.LogDebug(
            "Заметки чата {ChatId}: удалена {PinType} -> {PinId} ({Reason})",
            sessionId,
            pin.Type,
            pinId,
            Shorten(Sanitize(arguments.Reason)));

        _journal.Add(new PinJournalEntry(PinAction.Deleted, pin.Type, pin.Content));

        return ToolJson.Serialize(new PinActionReturn(
            "Ok",
            "Заметка удалена и в итоговый разбор не попадёт. " +
            "Само наблюдение уже вошло в накопительную сводку по заявке и оттуда не исчезает, " +
            "поэтому удалять заметку ради того, чтобы «забыть» факт, бессмысленно."));
    }

    public async Task<string> SearchPins(SearchPinsArguments arguments)
    {
        var query = Sanitize(arguments.Query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("Пустой поисковый запрос.");
        }

        if (!TryParseOptionalPinType(arguments.PinType, out var filter))
        {
            return Error($"Неизвестная категория заметки: \"{arguments.PinType}\". " +
                         "Допустимые значения: WhatToCheck, Summary, Mistake, Attention.");
        }

        if (_guard.IsRepeat("search_pins", query, arguments.PinType))
        {
            return RepeatCallGuard.RepeatResponse("search_pins");
        }

        var limit = arguments.Limit <= 0 ? DefaultSearchLimit : Math.Min(arguments.Limit, MaxSearchLimit);

        var matched = (await repository.KnnSearchPinsAsync(sessionId, query, limit, filter, searchDistance))
            .Select(match => ToView(match.Pin))
            .ToList();

        logger.LogDebug(
            "Заметки чата {ChatId}: поиск \"{Query}\" -> {Matched} совпадений",
            sessionId,
            Shorten(query),
            matched.Count);

        return ToolJson.Serialize(new GetPinsReturn("Ok", matched.Count, matched));
    }

    public async Task<string> GetPins(GetPinsArguments arguments)
    {
        if (!TryParseOptionalPinType(arguments.PinType, out var filter))
        {
            return Error($"Неизвестная категория заметки: \"{arguments.PinType}\". " +
                         "Допустимые значения: WhatToCheck, Summary, Mistake, Attention.");
        }

        var all = (await repository.GetPinsAsync(sessionId, filter)).ToList();
        var pins = all.TakeLast(MaxGetPinsLimit).Select(ToView).ToList();

        logger.LogDebug("Заметки чата {ChatId}: выдано {Returned} из {Total}", sessionId, pins.Count, all.Count);

        return ToolJson.Serialize(new GetPinsReturn("Ok", all.Count, pins));
    }

    private PinType Downgrade(PinType requested, string content)
    {
        if (requested is not (PinType.Mistake or PinType.Attention)) return requested;

        if (AnonymizationTag.IsMatch(content)) return PinType.WhatToCheck;

        return scope == FindingScope.Fragment && AbsenceWording.IsMatch(content)
            ? PinType.WhatToCheck
            : requested;
    }

    private static string DowngradeNote(PinType requested, PinType stored, string content)
    {
        if (requested == stored) return "Заметка создана.";

        return AnonymizationTag.IsMatch(content)
            ? $"Заметка записана как {stored}, а не {requested}: она опирается на данные, вырезанные " +
              "анонимизацией. В самой заявке эти поля заполнены, скрыты они только от тебя, " +
              "поэтому ошибкой заявки это не является."
            : $"Заметка записана как {stored}, а не {requested}: она говорит, что чего-то нет, " +
              "а ты видишь лишь один фрагмент заявки — остальные читаются отдельно. " +
              "Утверждать об отсутствии можно только по всей заявке целиком.";
    }

    private static PinView ToView(Pin pin) =>
        new(pin.Id,
            pin.Type.ToString(),
            pin.Content.Length <= MaxPinContentChars ? pin.Content : pin.Content[..MaxPinContentChars] + "...");

    private static bool TryParsePinType(string? raw, out PinType pinType) =>
        Enum.TryParse(raw?.Trim(), ignoreCase: true, out pinType) && Enum.IsDefined(pinType);

    private static bool TryParseOptionalPinType(string? raw, out PinType? pinType)
    {
        pinType = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        if (!TryParsePinType(raw, out var parsed)) return false;

        pinType = parsed;
        return true;
    }

    private static string Sanitize(string? value) =>
        value is null ? string.Empty : ServiceTokens.Replace(value, " ").Trim();

    private static string Shorten(string value) =>
        value.Length <= 60 ? value : value[..60] + "...";

    private static string Error(string message) =>
        ToolJson.Serialize(new PinActionReturn("Error", message));
}
