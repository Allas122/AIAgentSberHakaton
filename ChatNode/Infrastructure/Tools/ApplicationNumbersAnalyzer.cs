using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatNode.Infrastructure.Tools;

public static partial class ApplicationNumbersAnalyzer
{
    private const decimal MoneyTolerance = 1m;
    private const int MaxNameChars = 55;
    private const int MaxLineAlerts = 5;

    private sealed record BudgetItem(string Category, string Kind, string Name, decimal? Quantity, decimal? Price, decimal Sum);

    private sealed record EventPlan(decimal Participants, decimal Publications, decimal Views);

    private sealed record MediaPlan(decimal Publications, decimal Views);

    public static string Analyze(IReadOnlyList<string> rawLines)
    {
        if (rawLines.Count == 0) return string.Empty;

        var lines = rawLines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var goalText = ReadGoalText(lines);

        var items = ReadBudget(lines);
        var declaredTotal = ReadDeclaredTotal(lines);
        var redactedPlans = new List<string>();
        var planned = ReadPlanned(lines, redactedPlans);
        var events = ReadEvents(lines);
        var media = ReadMedia(lines);
        var cofinancing = ReadCofinancing(lines);

        var report = new StringBuilder();
        var alerts = new List<string>();

        AppendBudget(report, alerts, items, declaredTotal);
        AppendResults(report, alerts, planned, events, media, goalText, redactedPlans);
        AppendRatios(report, items, declaredTotal, planned, cofinancing);

        if (report.Length == 0) return string.Empty;

        if (alerts.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("РАСХОЖДЕНИЯ:");
            foreach (var alert in alerts) report.AppendLine($"- {alert}");
        }

        return report.ToString().TrimEnd();
    }

    private static void AppendBudget(
        StringBuilder report,
        List<string> alerts,
        IReadOnlyList<BudgetItem> items,
        decimal? declaredTotal)
    {
        if (items.Count == 0) return;

        var calculated = items.Sum(item => item.Sum);

        report.AppendLine("СМЕТА:");
        report.AppendLine($"- строк в смете: {items.Count}, сумма по строкам: {Money(calculated)}");

        if (declaredTotal is { } declared)
        {
            report.AppendLine($"- заявленная общая сумма: {Money(declared)}");

            if (Math.Abs(calculated - declared) > MoneyTolerance)
            {
                alerts.Add(
                    $"сумма строк сметы ({Money(calculated)}) не сходится с заявленной общей суммой " +
                    $"({Money(declared)}), разница {Money(Math.Abs(calculated - declared))}");
            }
        }

        var mismatched = items.Where(HasLineMismatch).ToList();

        foreach (var item in mismatched.Take(MaxLineAlerts))
        {
            alerts.Add(
                $"позиция «{Shorten(item.Name)}»: {Money(item.Price!.Value)} × {item.Quantity!.Value:0.##} = " +
                $"{Money(LineTotal(item)!.Value)}, а суммой указано {Money(item.Sum)}");
        }

        if (mismatched.Count > MaxLineAlerts)
        {
            alerts.Add($"ещё несходящихся позиций сметы: {mismatched.Count - MaxLineAlerts}");
        }

        if (calculated <= 0) return;

        var byCategory = items
            .GroupBy(item => item.Category)
            .Select(group => (Category: group.Key, Sum: group.Sum(item => item.Sum)))
            .OrderByDescending(entry => entry.Sum)
            .ToList();

        report.AppendLine("- доли категорий:");
        foreach (var (category, sum) in byCategory)
        {
            report.AppendLine($"  {Percent(sum, calculated)} — {category}: {Money(sum)}");
        }

        report.AppendLine("- позиции сметы:");
        foreach (var group in byCategory)
        {
            report.AppendLine($"  {group.Category}:");

            foreach (var item in items.Where(item => item.Category == group.Category)
                         .OrderByDescending(item => item.Sum))
            {
                report.AppendLine($"    {UnitPrefix(item)}{Money(item.Sum)} [{item.Kind}] {Shorten(item.Name)}");
            }
        }
    }

    private static decimal? LineTotal(BudgetItem item) =>
        item.Price is { } price && item.Quantity is { } quantity && quantity > 0
            ? price * quantity
            : null;

    private static bool HasLineMismatch(BudgetItem item) =>
        LineTotal(item) is { } expected && Math.Abs(expected - item.Sum) > MoneyTolerance;

    private static string UnitPrefix(BudgetItem item)
    {
        if (LineTotal(item) is not { } expected) return string.Empty;

        var head = $"{Money(item.Price!.Value)} × {item.Quantity!.Value:0.##} = ";

        return HasLineMismatch(item) ? $"{head}{Money(expected)} ≠ " : head;
    }

    private static void AppendResults(
        StringBuilder report,
        List<string> alerts,
        IReadOnlyDictionary<string, decimal> planned,
        IReadOnlyList<EventPlan> events,
        IReadOnlyList<MediaPlan> media,
        string goalText,
        IReadOnlyList<string> redactedPlans)
    {
        if (planned.Count == 0 && events.Count == 0) return;

        report.AppendLine();
        report.AppendLine("ПОКАЗАТЕЛИ:");

        var plannedEvents = Find(planned, "мероприят");
        var plannedParticipants = Find(planned, "участник");
        var plannedPublications = Find(planned, "публикац");
        var plannedViews = Find(planned, "просмотр");

        if (events.Count > 0)
        {
            var participants = events.Sum(item => item.Participants);
            var publications = events.Sum(item => item.Publications);
            var views = events.Sum(item => item.Views);

            report.AppendLine(
                $"- календарный план: мероприятий {events.Count}, участников {participants:0.##}, " +
                $"публикаций {publications:0.##}, просмотров {views:0.##}");

            Compare(alerts, "количество мероприятий", plannedEvents, events.Count);
            Compare(alerts, "количество участников", plannedParticipants, participants);
            Compare(alerts, "количество публикаций", plannedPublications, publications);
            Compare(alerts, "количество просмотров", plannedViews, views);
        }

        if (planned.Count > 0)
        {
            report.AppendLine("- плановые значения из раздела «Результаты»:");
            foreach (var (name, value) in planned) report.AppendLine($"  {value:0.##} — {Shorten(name)}");
        }

        if (redactedPlans.Count > 0)
        {
            report.AppendLine(
                "- ВНИМАНИЕ: часть плановых значений скрыта обезличиванием, сверить их нельзя: " +
                string.Join("; ", redactedPlans.Select(Shorten)));
        }

        if (media.Count > 0)
        {
            var publications = media.Sum(item => item.Publications);
            var views = media.Sum(item => item.Views);

            report.AppendLine($"- медиаплан: ресурсов {media.Count}, публикаций {publications:0.##}, охват {views:0.##}");

            Compare(alerts, "охват публикаций (медиаплан против результатов)", plannedViews, views);
        }

        AppendGoalPromises(alerts, goalText, plannedParticipants);
    }

    private static void AppendGoalPromises(List<string> alerts, string goalText, decimal? plannedParticipants)
    {
        if (goalText.Length == 0 || plannedParticipants is not { } participants) return;

        foreach (Match match in AtLeastRegex().Matches(goalText))
        {
            if (!decimal.TryParse(match.Groups[1].Value, out var promised)) continue;
            if (promised <= participants) continue;

            alerts.Add(
                $"в цели обещано «не менее {promised:0.##}» участников, а в плановых результатах " +
                $"указано {participants:0.##}");
        }
    }

    private static void AppendRatios(
        StringBuilder report,
        IReadOnlyList<BudgetItem> items,
        decimal? declaredTotal,
        IReadOnlyDictionary<string, decimal> planned,
        IReadOnlyList<decimal> cofinancing)
    {
        var total = declaredTotal ?? (items.Count > 0 ? items.Sum(item => item.Sum) : 0m);
        if (total <= 0) return;

        report.AppendLine();
        report.AppendLine("СООТНОШЕНИЯ:");

        if (Find(planned, "участник") is { } participants && participants > 0)
        {
            report.AppendLine($"- запрошено на одного участника: {Money(total / participants)}");
        }

        if (cofinancing.Count > 0)
        {
            var own = cofinancing.Sum();
            report.AppendLine(
                $"- софинансирование: {Money(own)} по {cofinancing.Count} записям, " +
                $"это {Percent(own, total)} от запрошенной суммы");
        }
    }

    private static void Compare(List<string> alerts, string what, decimal? plannedValue, decimal actual)
    {
        if (plannedValue is not { } expected) return;
        if (Math.Abs(expected - actual) < 0.5m) return;

        alerts.Add($"{what}: в плановых значениях {expected:0.##}, а по разделам заявки {actual:0.##}");
    }

    private static decimal? Find(IReadOnlyDictionary<string, decimal> planned, string keyword)
    {
        foreach (var (name, value) in planned)
        {
            var head = name.Length <= 30 ? name : name[..30];
            if (head.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return value;
        }

        return null;
    }

    private static string ReadGoalText(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("Основная цель проекта", StringComparison.OrdinalIgnoreCase)) continue;

            var inline = After(lines[i]);
            if (inline.Length > 0) return inline;

            return i + 1 < lines.Count ? lines[i + 1] : string.Empty;
        }

        return string.Empty;
    }

    private static List<BudgetItem> ReadBudget(IReadOnlyList<string> lines)
    {
        var items = new List<BudgetItem>();

        string? category = null;
        string? kind = null;
        string? name = null;
        decimal? quantity = null;
        decimal? price = null;

        foreach (var line in lines)
        {
            var categoryMatch = CategoryRegex().Match(line);
            if (categoryMatch.Success)
            {
                category = categoryMatch.Groups[1].Value;
                continue;
            }

            var kindMatch = KindRegex().Match(line);
            if (kindMatch.Success)
            {
                kind = kindMatch.Groups[1].Value;
                continue;
            }

            if (StartsWith(line, "Название:")) name = After(line);
            else if (StartsWith(line, "Количество:")) quantity = ParseNumber(After(line));
            else if (StartsWith(line, "Цена:")) price = ParseNumber(After(line));
            else if (StartsWith(line, "Сумма:") && category is not null)
            {
                var sum = ParseNumber(After(line));
                if (sum is { } value)
                {
                    items.Add(new BudgetItem(category, kind ?? "—", name ?? "без названия", quantity, price, value));
                }

                name = null;
                quantity = null;
                price = null;
            }
        }

        return items;
    }

    private static decimal? ReadDeclaredTotal(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("Общая сумма расходов", StringComparison.OrdinalIgnoreCase)) continue;

            var inline = ParseNumber(After(lines[i]));
            if (inline is not null) return inline;

            if (i + 1 < lines.Count) return ParseNumber(lines[i + 1]);
        }

        return null;
    }

    private static Dictionary<string, decimal> ReadPlanned(IReadOnlyList<string> lines, List<string> redacted)
    {
        var planned = new Dictionary<string, decimal>(StringComparer.Ordinal);
        string? block = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var blockMatch = BlockRegex().Match(lines[i]);
            if (blockMatch.Success)
            {
                block = blockMatch.Groups[1].Value;
                continue;
            }

            if (block is null || !lines[i].StartsWith("Плановое количество", StringComparison.OrdinalIgnoreCase)) continue;

            var raw = After(lines[i]);
            if (raw.Length == 0 && i + 1 < lines.Count) raw = lines[i + 1];

            if (RedactionRegex().IsMatch(raw.Trim()))
            {
                redacted.Add(block);
                continue;
            }

            if (ParseNumber(raw) is { } number) planned.TryAdd(block, number);
        }

        return planned;
    }

    private static List<EventPlan> ReadEvents(IReadOnlyList<string> lines)
    {
        var events = new List<EventPlan>();

        decimal participants = 0;
        decimal publications = 0;
        decimal views = 0;
        var open = false;

        void Flush()
        {
            if (!open) return;

            events.Add(new EventPlan(participants, publications, views));
            participants = publications = views = 0;
            open = false;
        }

        foreach (var line in lines)
        {
            if (StartsWith(line, "Количество уникальных участников:"))
            {
                Flush();
                participants = ParseNumber(After(line)) ?? 0;
                open = true;
            }
            else if (open && StartsWith(line, "Количество публикаций:"))
            {
                publications = ParseNumber(After(line)) ?? 0;
            }
            else if (open && StartsWith(line, "Количество просмотров:"))
            {
                views = ParseNumber(After(line)) ?? 0;
            }
        }

        Flush();

        return events;
    }

    private static List<MediaPlan> ReadMedia(IReadOnlyList<string> lines)
    {
        var media = new List<MediaPlan>();

        decimal publications = 0;
        var open = false;

        foreach (var line in lines)
        {
            if (StartsWith(line, "Планируемое количество публикаций в данном ресурсе:"))
            {
                if (open) media.Add(new MediaPlan(publications, 0));

                publications = ParseNumber(After(line)) ?? 0;
                open = true;
            }
            else if (open && StartsWith(line, "Планируемое итоговое количество просмотров"))
            {
                media.Add(new MediaPlan(publications, ParseNumber(After(line)) ?? 0));
                publications = 0;
                open = false;
            }
        }

        if (open) media.Add(new MediaPlan(publications, 0));

        return media;
    }

    private static List<decimal> ReadCofinancing(IReadOnlyList<string> lines) =>
        lines
            .Where(line => StartsWith(line, "Сумма, руб.:"))
            .Select(line => ParseNumber(After(line)))
            .Where(value => value is > 0)
            .Select(value => value!.Value)
            .ToList();

    private static bool StartsWith(string line, string label) =>
        line.StartsWith(label, StringComparison.OrdinalIgnoreCase);

    private static string After(string line)
    {
        var index = line.IndexOf(':');
        return index >= 0 && index + 1 < line.Length ? line[(index + 1)..].Trim() : string.Empty;
    }

    private static decimal? ParseNumber(string raw)
    {
        if (raw.Length == 0) return null;
        if (RedactionRegex().IsMatch(raw.Trim())) return null;

        var cleaned = raw
            .Replace(' ', ' ')
            .Replace(" ", string.Empty)
            .Replace(",", ".");

        var match = NumberRegex().Match(cleaned);

        return match.Success && decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string Money(decimal value) =>
        value.ToString("#,##0.##", CultureInfo.InvariantCulture).Replace(',', ' ') + " руб.";

    private static string Percent(decimal part, decimal whole) =>
        whole <= 0 ? "—" : (part / whole * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string Shorten(string text) =>
        text.Length <= MaxNameChars ? text : text[..MaxNameChars] + "...";

    [GeneratedRegex(@"^Категория\s+""(.+)""$")]
    private static partial Regex CategoryRegex();

    [GeneratedRegex(@"^Тип\s+""(.+)""$")]
    private static partial Regex KindRegex();

    [GeneratedRegex(@"^Блок\s+""(.+)""$")]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"не менее\s+(\d+)")]
    private static partial Regex AtLeastRegex();

    [GeneratedRegex(@"^\[[A-Z][A-Z_]*_[0-9A-Fa-f]{2,8}\]$")]
    private static partial Regex RedactionRegex();
}
