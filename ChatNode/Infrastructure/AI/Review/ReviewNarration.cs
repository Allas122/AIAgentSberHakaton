using System.Text;
using System.Text.RegularExpressions;
using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Review;

public static class ReviewNarration
{
    public const string SummaryMarker = "ИТОГ:";

    private const int MaxFindingChars = 400;
    private const int MaxExplanationChars = 1200;

    private static readonly Regex Line = new(@"^\s*(\d{1,2})\s*[:.)]\s*(.+)$", RegexOptions.Compiled);

    public static string SystemPrompt => """
Ты — эксперт, проверивший заявку на грант. Решения уже приняты: вывод и балл по каждому критерию
посчитаны и изменению не подлежат. Твоя задача — только объяснить их человеческим языком.

Формат ответа — строго по строке на критерий:
номер: объяснение одним-двумя предложениями

После всех критериев одной строкой:
ИТОГ: 2-4 предложения о том, можно ли подавать заявку и что исправить в первую очередь.

Правила:
- Никакого текста вне этих строк. Ни заголовков, ни markdown, ни списков.
- Объясняй ровно тот вывод, который уже стоит у критерия. Не спорь с ним и не меняй балл.
- Опирайся только на перечисленные замечания. Ничего не додумывай и не обобщай «по смыслу».
- Строки с пометкой «посчитано по документу» — это проверенные расчётом факты.
  Числа из них переноси дословно, до единой цифры, без округления и пересказа.
- Сам ничего не считай и не сравнивай числа между собой: всё, что нужно, уже посчитано.
- «Раздел не найден» означает, что автоматическая сверка оглавления не нашла подходящего
  раздела. Так и пиши — как повод проверить вручную, а не как доказанное отсутствие.
- Если по критерию замечаний нет, честно скажи, что нарушений не зафиксировано, и не выдумывай
  достоинств заявки.
- Теги вида [SURNAME_0001] переноси побуквенно, они будут восстановлены автоматически.
""";

    public static string BuildUserMessage(ApplicationReviewResult review, string? userComment)
    {
        var sb = new StringBuilder();

        foreach (var outcome in review.Criteria)
        {
            sb.AppendLine(
                $"{outcome.Criterion.Index}. {outcome.Criterion.Name} — " +
                $"{ReviewRenderer.StatusText(outcome.Status)}, " +
                $"балл {outcome.Score} из {outcome.Criterion.MaxScore}");

            if (outcome.Findings.Count == 0)
            {
                sb.AppendLine("   замечаний не зафиксировано");
            }
            else
            {
                foreach (var finding in outcome.Findings)
                {
                    sb.AppendLine($"   - {Tag(finding)}{Shorten(finding.Content)}");
                }
            }

            sb.AppendLine();
        }

        if (review.Unassigned.Count > 0)
        {
            sb.AppendLine("Замечания вне критериев (в объяснения не включай, учти в итоге):");
            foreach (var finding in review.Unassigned)
            {
                sb.AppendLine($"   - {Tag(finding)}{Shorten(finding.Content)}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(userComment))
        {
            sb.AppendLine($"Пользователь просил обратить внимание: {Shorten(userComment)}");
        }

        return sb.ToString().TrimEnd();
    }

    public static void Apply(ApplicationReviewResult review, string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return;

        var byIndex = review.Criteria.ToDictionary(o => o.Criterion.Index);

        foreach (var raw in response.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith(SummaryMarker, StringComparison.OrdinalIgnoreCase))
            {
                review.Summary = Cut(line[SummaryMarker.Length..].Trim());
                continue;
            }

            var match = Line.Match(line);
            if (!match.Success) continue;

            if (!int.TryParse(match.Groups[1].Value, out var index)) continue;
            if (!byIndex.TryGetValue(index, out var outcome)) continue;

            outcome.Explanation = Cut(match.Groups[2].Value.Trim());
        }
    }

    private static string Tag(ReviewFinding finding) => finding.Source switch
    {
        FindingSource.Computed => "[посчитано по документу] ",
        _ => finding.Type switch
        {
            PinType.Mistake => "[нарушение] ",
            PinType.Attention => "[спорно] ",
            PinType.WhatToCheck => "[проверить] ",
            _ => string.Empty
        }
    };

    private static string Shorten(string value) =>
        value.Length <= MaxFindingChars ? value : value[..MaxFindingChars] + "…";

    private static string Cut(string value) =>
        value.Length <= MaxExplanationChars ? value : value[..MaxExplanationChars] + "…";
}
