using System.Globalization;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.AI.Services;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class AssignmentFunctionToolsSet(
    IAssignmentRepository repository,
    IUserRepository userRepository,
    IAnonymizeClient anonymizeClient,
    Guid ownerId,
    string sessionId,
    AssignmentSource sourceKind,
    ILogger logger,
    string? sourceRef = null) : IFunctionToolsSet
{
    private readonly RepeatCallGuard _guard = new();
    private readonly List<AssignmentView> _created = [];

    private const int MaxListLimit = 10;
    private const int MaxTitleChars = 300;
    private const int MaxDescriptionChars = 8000;
    private const int MaxAssigneeChars = 200;
    private const int MinSurnameChars = 4;

    private static readonly UserRole[] StaffRoles = [UserRole.Rector, UserRole.Coordinator];

    private static readonly string[] StatusValues =
        ["New", "InProgress", "Blocked", "Done", "Cancelled"];

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd.MM.yyyy", "dd.MM.yy", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm"];

    private static readonly Regex ServiceTokens = new(@"<\|[^|>]*\|>", RegexOptions.Compiled);

    private static readonly Regex RussianDate = new(
        @"(\d{1,2})\s+([А-Яа-яЁё]+)\s+(\d{4})",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, int> RussianMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["январ"] = 1, ["феврал"] = 2, ["март"] = 3, ["апрел"] = 4,
        ["ма"] = 5, ["июн"] = 6, ["июл"] = 7, ["август"] = 8,
        ["сентябр"] = 9, ["октябр"] = 10, ["ноябр"] = 11, ["декабр"] = 12
    };

    private const string StatusDescription =
        "Состояние: New — заведено, к работе не приступали; InProgress — в работе; " +
        "Blocked — застопорилось, нужна чужая реакция; Done — исполнено; Cancelled — отменено.";

    public IReadOnlyList<AssignmentView> Created => _created;

    private string SourceWord => sourceKind switch
    {
        AssignmentSource.Letter => "письма",
        AssignmentSource.Application => "разбора заявки",
        _ => "просьбы пользователя"
    };

    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<CreateAssignmentArguments>(
            name: "create_assignment",
            description: $"Завести поручение, вытекающее из {SourceWord}. " +
                         $"По одному вызову на каждое поручение, формулировки брать из {SourceWord}.",
            handler: CreateAssignment,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["title"] = FunctionParameter.String(
                        "Краткая формулировка: что именно нужно сделать. Одно предложение. " +
                        "БЕЗ срока и БЕЗ фамилии исполнителя — для них есть отдельные поля."),
                    ["description"] = FunctionParameter.String(
                        $"Полная формулировка из {SourceWord} с деталями и условиями."),
                    ["assignee"] = FunctionParameter.String(
                        $"Исполнитель, если он назван в {SourceWord}. Фамилия и инициалы или должность. " +
                        $"Заполняй ВСЕГДА, когда в {SourceWord} указано, кто отвечает."),
                    ["due_date"] = FunctionParameter.String(
                        $"Срок исполнения. Заполняй ВСЕГДА, когда в {SourceWord} есть дата. " +
                        "Пиши как есть: «15 сентября 2026» или 2026-09-15 — оба формата понимаются. " +
                        "Не указывай только если срока действительно нет.")
                },
                required: ["title", "description"]
            )
        ),
        FunctionTool.Create<ListAssignmentsArguments>(
            name: "list_assignments",
            description: $"Показать поручения пользователя, не больше {MaxListLimit}. " +
                         "Сначала самые свежие.",
            handler: ListAssignments,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["status"] = FunctionParameter.String(
                        "Фильтр по состоянию, необязателен. " + StatusDescription, StatusValues),
                    ["limit"] = FunctionParameter.Integer($"Сколько вернуть, от 1 до {MaxListLimit}.")
                }
            )
        ),
        FunctionTool.Create<UpdateAssignmentStatusArguments>(
            name: "update_assignment_status",
            description: "Сменить состояние поручения.",
            handler: UpdateAssignmentStatus,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["assignment_id"] = FunctionParameter.String("ID поручения из list_assignments."),
                    ["status"] = FunctionParameter.String(StatusDescription, StatusValues),
                    ["comment"] = FunctionParameter.String("Чем вызвана смена состояния.")
                },
                required: ["assignment_id", "status"]
            )
        )
    ];

    public async Task<string> CreateAssignment(CreateAssignmentArguments arguments)
    {
        var title = Sanitize(arguments.Title, MaxTitleChars);

        if (string.IsNullOrWhiteSpace(title))
        {
            return Error("Формулировка поручения пустая, заводить нечего.");
        }

        if (_guard.IsRepeat("create_assignment", title))
        {
            return RepeatCallGuard.RepeatResponse("create_assignment");
        }

        if (!TryParseDueDate(arguments.DueDate, out var dueDate))
        {
            return Error($"Не понимаю срок \"{arguments.DueDate}\". " +
                         "Укажи его как ГГГГ-ММ-ДД или ДД.ММ.ГГГГ, либо не указывай вовсе.");
        }

        var restoredTitle = await RestoreAsync(title);
        var restoredDescription = await RestoreAsync(Sanitize(arguments.Description, MaxDescriptionChars));
        var restoredAssignee = await RestoreAsync(Sanitize(arguments.Assignee, MaxAssigneeChars));

        if (AnonymizationTags.Contains(restoredTitle) ||
            AnonymizationTags.Contains(restoredDescription) ||
            AnonymizationTags.Contains(restoredAssignee))
        {
            logger.LogWarning(
                "Поручения сессии {SessionId}: \"{Title}\" отклонено — в тексте остался нераскрытый тег",
                sessionId,
                title);

            return Error(
                "В тексте остался тег персональных данных, раскрыть его не удалось — " +
                "в поручении его показывать нельзя. Перепиши формулировку без тега: " +
                "назови человека по роли («заявителю», «руководителю проекта») или убери упоминание. " +
                "Остальную часть формулировки сохрани.");
        }

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = restoredTitle,
            Description = restoredDescription,
            Assignee = restoredAssignee,
            DueDate = dueDate,
            Status = AssignmentStatus.New,
            SourceKind = sourceKind,
            SourceRef = sourceRef,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (string.IsNullOrWhiteSpace(assignment.Assignee)) assignment.Assignee = null;

        if (await MatchCuratorAsync(assignment.Assignee) is { } curator)
        {
            assignment.AssigneeId = curator.Id;
            assignment.Assignee = curator.Name;
        }

        var assignmentId = await repository.CreateAsync(assignment);

        _created.Add(new AssignmentView(
            assignmentId,
            assignment.Title,
            assignment.Assignee,
            Format(assignment.DueDate),
            assignment.Status.ToString()));

        return ToolJson.Serialize(new CreateAssignmentReturn(
            "Ok",
            "Поручение заведено. Не заводи его повторно другими словами.",
            assignmentId));
    }

    public async Task<string> ListAssignments(ListAssignmentsArguments arguments)
    {
        if (!TryParseOptionalStatus(arguments.Status, out var status))
        {
            return Error($"Неизвестное состояние: \"{arguments.Status}\". " +
                         "Допустимые значения: New, InProgress, Blocked, Done, Cancelled.");
        }

        if (_guard.IsRepeat("list_assignments", arguments.Status, arguments.Limit.ToString()))
        {
            return RepeatCallGuard.RepeatResponse("list_assignments");
        }

        var limit = arguments.Limit <= 0 ? MaxListLimit : Math.Min(arguments.Limit, MaxListLimit);

        var assignments = await repository.ListAsync(ownerId, status, limit, 0);
        var total = await repository.CountAsync(ownerId, status);

        var views = await HideAsync(assignments);

        return ToolJson.Serialize(new ListAssignmentsReturn("Ok", total, views));
    }

    public async Task<string> UpdateAssignmentStatus(UpdateAssignmentStatusArguments arguments)
    {
        if (!Guid.TryParse(arguments.AssignmentId, out var assignmentId))
        {
            return Error($"\"{arguments.AssignmentId}\" не является идентификатором поручения.");
        }

        if (!TryParseStatus(arguments.Status, out var status))
        {
            return Error($"Неизвестное состояние: \"{arguments.Status}\". " +
                         "Допустимые значения: New, InProgress, Blocked, Done, Cancelled.");
        }

        if (_guard.IsRepeat("update_assignment_status", arguments.AssignmentId, arguments.Status))
        {
            return RepeatCallGuard.RepeatResponse("update_assignment_status");
        }

        var assignment = await repository.GetAsync(assignmentId);

        if (assignment is null || assignment.OwnerId != ownerId)
        {
            return Error($"Поручение {assignmentId} не найдено.");
        }

        var previous = assignment.Status;
        assignment.Status = status;

        if (!await repository.UpdateAsync(assignment))
        {
            return Error($"Поручение {assignmentId} не найдено.");
        }

        return ToolJson.Serialize(new AssignmentActionReturn(
            "Ok",
            $"Состояние изменено: {previous} -> {status}."));
    }

    private async Task<UserAccount?> MatchCuratorAsync(string? assignee)
    {
        if (string.IsNullOrWhiteSpace(assignee)) return null;

        var mentioned = Tokenize(assignee);
        if (mentioned.Count == 0) return null;

        var staff = (await userRepository.ListAccountsAsync())
            .Where(account => StaffRoles.Contains(account.Role))
            .Where(account => Mentions(account, mentioned))
            .ToList();

        if (staff.Count == 1) return staff[0];

        if (staff.Count > 1)
        {
            logger.LogWarning(
                "Поручения сессии {SessionId}: \"{Assignee}\" подходит сразу {Matched} сотрудникам — " +
                "оставляю поручение без привязки к учётной записи",
                sessionId,
                assignee,
                staff.Count);
        }

        return null;
    }

    private static bool Mentions(UserAccount account, IReadOnlyList<string> mentioned) =>
        Tokenize(account.Name)
            .Where(part => part.Length >= MinSurnameChars)
            .Any(part => mentioned.Any(token => SameName(part, token)));

    private static bool SameName(string part, string token)
    {
        if (token.Length < MinSurnameChars) return false;
        if (token.Length > part.Length + 3) return false;

        var common = 0;
        while (common < part.Length && common < token.Length && part[common] == token[common]) common++;

        return common >= part.Length - (part.Length <= 6 ? 1 : 2);
    }

    private static List<string> Tokenize(string value) =>
        value
            .ToLowerInvariant()
            .Replace('ё', 'е')
            .Split(s_nameSeparators, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

    private static readonly char[] s_nameSeparators =
        [' ', '\t', '\n', '\r', ',', '.', ';', ':', '(', ')', '«', '»', '"', '-', '—', '/'];

    private async Task<IReadOnlyList<AssignmentView>> HideAsync(IReadOnlyList<Assignment> assignments)
    {
        if (assignments.Count == 0) return [];

        var texts = new List<string>(assignments.Count * 2);
        foreach (var assignment in assignments)
        {
            texts.Add(assignment.Title);
            texts.Add(assignment.Assignee ?? string.Empty);
        }

        var hidden = await anonymizeClient.AnonymizeBatchAsync(texts, sessionId);

        return assignments
            .Select((assignment, index) => new AssignmentView(
                assignment.Id,
                hidden[index * 2],
                string.IsNullOrWhiteSpace(hidden[index * 2 + 1]) ? null : hidden[index * 2 + 1],
                Format(assignment.DueDate),
                assignment.Status.ToString()))
            .ToList();
    }

    private async Task<string> RestoreAsync(string text) =>
        string.IsNullOrWhiteSpace(text) ? text : await anonymizeClient.DeanonymizeAsync(text, sessionId);

    private static bool TryParseDueDate(string? raw, out DateTimeOffset? dueDate)
    {
        dueDate = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var value = raw.Trim();

        if (TryParseRussian(value, out var russian))
        {
            dueDate = russian;
            return true;
        }

        if (DateTimeOffset.TryParseExact(
                value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var exact))
        {
            dueDate = exact;
            return true;
        }

        if (DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var loose))
        {
            dueDate = loose;
            return true;
        }

        return false;
    }

    private static bool TryParseRussian(string value, out DateTimeOffset date)
    {
        date = default;

        var match = RussianDate.Match(value);
        if (!match.Success) return false;

        var monthWord = match.Groups[2].Value;
        var month = RussianMonths
            .Where(pair => monthWord.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => (int?)pair.Value)
            .FirstOrDefault();

        if (month is null) return false;

        if (!int.TryParse(match.Groups[1].Value, out var day)) return false;
        if (!int.TryParse(match.Groups[3].Value, out var year)) return false;
        if (day < 1 || day > DateTime.DaysInMonth(year, month.Value)) return false;

        date = new DateTimeOffset(year, month.Value, day, 0, 0, 0, TimeSpan.Zero);
        return true;
    }

    private static string? Format(DateTimeOffset? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryParseStatus(string? raw, out AssignmentStatus status) =>
        Enum.TryParse(raw?.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status);

    private static bool TryParseOptionalStatus(string? raw, out AssignmentStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        if (!TryParseStatus(raw, out var parsed)) return false;

        status = parsed;
        return true;
    }

    private static string Sanitize(string? value, int maxChars)
    {
        if (value is null) return string.Empty;

        var cleaned = ServiceTokens.Replace(value, " ").Trim();

        return cleaned.Length <= maxChars ? cleaned : cleaned[..maxChars];
    }

    private static string Error(string message) =>
        ToolJson.Serialize(new AssignmentActionReturn("Error", message));
}
