namespace ChatNode.Infrastructure.AI.Functions.ReturnModels;

public record CurrentDateReturn(
    string Today,
    string Weekday,
    string Time,
    string Timezone,
    string EndOfWeek,
    string EndOfMonth,
    string Tomorrow);

public record ResolvedDateReturn(string Date, string Weekday, int DaysFromToday, string Explanation);

public record CalendarErrorReturn(string Status, string Message);

public record DateDifferenceReturn(
    string From,
    string To,
    int Days,
    int BusinessDays,
    int FullWeeks,
    int FullMonths,
    bool InPast,
    string Explanation);
