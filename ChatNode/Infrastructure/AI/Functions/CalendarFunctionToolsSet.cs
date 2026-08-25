using System.Globalization;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class CalendarFunctionToolsSet(TimeProvider clock, TimeZoneInfo timeZone) : IFunctionToolsSet
{
    private const int MaxOffset = 520;

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] AnchorValues =
    [
        "today", "tomorrow", "yesterday",
        "end_of_week", "start_of_next_week", "end_of_next_week",
        "end_of_month", "end_of_next_month",
        "end_of_quarter", "end_of_year",
        "plus_days", "plus_weeks", "plus_months", "plus_years", "plus_business_days"
    ];

    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<CurrentDateArguments>(
            name: "get_current_datetime",
            description:
                "Узнать сегодняшнюю дату, день недели и время. " +
                "Вызывай ВСЕГДА, прежде чем судить о сроках: что просрочено, что горит, " +
                "сколько осталось. Свою память о текущей дате не используй — она неверна.",
            handler: GetCurrentDate,
            parameters: FunctionParameter.Parameters(new Dictionary<string, FunctionParametersProperty>())
        ),
        FunctionTool.Create<ResolveDateArguments>(
            name: "resolve_date",
            description:
                "Превратить расплывчатый срок в конкретную дату: «конец недели», «завтра», " +
                "«через две недели», «к концу месяца». Возвращает дату в формате ГГГГ-ММ-ДД. " +
                "Считай через эту функцию, а не в уме — так не ошибёшься с длиной месяца.",
            handler: ResolveDate,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["anchor"] = FunctionParameter.String(
                        "Какой срок посчитать. end_of_week — ближайшее воскресенье; " +
                        "plus_days, plus_weeks, plus_months — сдвиг на offset единиц.",
                        AnchorValues),
                    ["offset"] = FunctionParameter.Integer(
                        "На сколько сдвинуть для plus_days, plus_weeks, plus_months. " +
                        "Для остальных якорей не нужен."),
                    ["from"] = FunctionParameter.String(
                        "От какой даты считать в формате ГГГГ-ММ-ДД. По умолчанию от сегодня.")
                },
                required: ["anchor"]
            )
        ),
        FunctionTool.Create<DateDifferenceArguments>(
            name: "date_difference",
            description:
                "Посчитать, сколько между двумя датами: дней, рабочих дней, полных недель и месяцев. " +
                "Через неё отвечай «сколько осталось до срока» и «на сколько просрочено». " +
                "Если from не задан, считает от сегодня. В уме не вычитай — ошибёшься на границах месяцев.",
            handler: DateDifference,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["to"] = FunctionParameter.String("Целевая дата в формате ГГГГ-ММ-ДД."),
                    ["from"] = FunctionParameter.String(
                        "Дата отсчёта в формате ГГГГ-ММ-ДД. По умолчанию сегодня.")
                },
                required: ["to"]
            )
        )
    ];

    public Task<string> GetCurrentDate(CurrentDateArguments _)
    {
        var now = Now();
        var today = DateOnly.FromDateTime(now.DateTime);

        return Task.FromResult(ToolJson.Serialize(new CurrentDateReturn(
            Iso(today),
            Weekday(today),
            now.ToString("HH:mm", CultureInfo.InvariantCulture),
            timeZone.Id,
            Iso(EndOfWeek(today)),
            Iso(EndOfMonth(today)),
            Iso(today.AddDays(1)))));
    }

    public Task<string> ResolveDate(ResolveDateArguments arguments)
    {
        var today = DateOnly.FromDateTime(Now().DateTime);

        var origin = today;
        if (!string.IsNullOrWhiteSpace(arguments.From)
            && !DateOnly.TryParseExact(arguments.From, "yyyy-MM-dd", out origin))
        {
            return Task.FromResult(ToolJson.Serialize(
                new CalendarErrorReturn("Error", "Дату в поле from пиши как ГГГГ-ММ-ДД, например 2026-09-15.")));
        }

        var anchor = (arguments.Anchor ?? string.Empty).Trim().ToLowerInvariant();
        var offset = Math.Clamp(arguments.Offset ?? 1, -MaxOffset, MaxOffset);

        var (resolved, explanation) = anchor switch
        {
            "today" => (origin, "сегодня"),
            "tomorrow" => (origin.AddDays(1), "завтра"),
            "yesterday" => (origin.AddDays(-1), "вчера"),
            "end_of_week" => (EndOfWeek(origin), "ближайшее воскресенье"),
            "start_of_next_week" => (EndOfWeek(origin).AddDays(1), "понедельник следующей недели"),
            "end_of_next_week" => (EndOfWeek(origin).AddDays(7), "воскресенье следующей недели"),
            "end_of_month" => (EndOfMonth(origin), "последний день текущего месяца"),
            "end_of_next_month" => (EndOfMonth(origin.AddMonths(1)), "последний день следующего месяца"),
            "end_of_quarter" => (EndOfQuarter(origin), "последний день квартала"),
            "end_of_year" => (new DateOnly(origin.Year, 12, 31), "последний день года"),
            "plus_days" => (origin.AddDays(offset), $"сдвиг на {offset} дн."),
            "plus_weeks" => (origin.AddDays(offset * 7), $"сдвиг на {offset} нед."),
            "plus_months" => (origin.AddMonths(offset), $"сдвиг на {offset} мес."),
            "plus_years" => (origin.AddYears(offset), $"сдвиг на {offset} г."),
            "plus_business_days" => (AddBusinessDays(origin, offset), $"сдвиг на {offset} раб. дн."),
            _ => (DateOnly.MinValue, string.Empty)
        };

        if (resolved == DateOnly.MinValue)
        {
            return Task.FromResult(ToolJson.Serialize(new CalendarErrorReturn("Error",
                $"Не знаю такого срока: «{arguments.Anchor}». Доступны: {string.Join(", ", AnchorValues)}.")));
        }

        return Task.FromResult(ToolJson.Serialize(new ResolvedDateReturn(
            Iso(resolved),
            Weekday(resolved),
            resolved.DayNumber - today.DayNumber,
            explanation)));
    }

    public Task<string> DateDifference(DateDifferenceArguments arguments)
    {
        if (!DateOnly.TryParseExact(arguments.To, "yyyy-MM-dd", out var target))
        {
            return Task.FromResult(ToolJson.Serialize(new CalendarErrorReturn("Error",
                $"Дату в поле to пиши как ГГГГ-ММ-ДД, а не «{arguments.To}».")));
        }

        var origin = DateOnly.FromDateTime(Now().DateTime);
        if (!string.IsNullOrWhiteSpace(arguments.From)
            && !DateOnly.TryParseExact(arguments.From, "yyyy-MM-dd", out origin))
        {
            return Task.FromResult(ToolJson.Serialize(new CalendarErrorReturn("Error",
                $"Дату в поле from пиши как ГГГГ-ММ-ДД, а не «{arguments.From}».")));
        }

        var days = target.DayNumber - origin.DayNumber;
        var inPast = days < 0;
        var span = Math.Abs(days);

        var explanation = days switch
        {
            0 => "это сегодня",
            > 0 => $"осталось {span} дн.",
            _ => $"просрочено на {span} дн."
        };

        return Task.FromResult(ToolJson.Serialize(new DateDifferenceReturn(
            Iso(origin),
            Iso(target),
            days,
            BusinessDaysBetween(origin, target),
            span / 7,
            FullMonthsBetween(origin, target),
            inPast,
            explanation)));
    }

    private static DateOnly AddBusinessDays(DateOnly date, int amount)
    {
        var step = amount < 0 ? -1 : 1;
        var left = Math.Abs(amount);

        while (left > 0)
        {
            date = date.AddDays(step);
            if (IsWorkday(date)) left--;
        }

        return date;
    }

    private static int BusinessDaysBetween(DateOnly from, DateOnly to)
    {
        var step = to < from ? -1 : 1;
        var counted = 0;

        for (var cursor = from; cursor != to; cursor = cursor.AddDays(step))
        {
            var next = cursor.AddDays(step);
            if (IsWorkday(next)) counted++;
        }

        return step < 0 ? -counted : counted;
    }

    private static int FullMonthsBetween(DateOnly from, DateOnly to)
    {
        var (early, late) = to < from ? (to, from) : (from, to);

        var months = (late.Year - early.Year) * 12 + late.Month - early.Month;
        if (late.Day < early.Day) months--;

        return to < from ? -months : months;
    }

    private static bool IsWorkday(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private DateTimeOffset Now() => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), timeZone);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Weekday(DateOnly date) =>
        Russian.DateTimeFormat.GetDayName(date.DayOfWeek);

    private static DateOnly EndOfWeek(DateOnly date)
    {
        var toSunday = ((int)DayOfWeek.Sunday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(toSunday);
    }

    private static DateOnly EndOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static DateOnly EndOfQuarter(DateOnly date)
    {
        var lastMonth = (int)Math.Ceiling(date.Month / 3.0) * 3;
        return new DateOnly(date.Year, lastMonth, DateTime.DaysInMonth(date.Year, lastMonth));
    }
}
