using System.Text;
using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Review;

public static class ReviewRenderer
{
    public static string ToMarkdown(ApplicationReviewResult review)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Итоговая оценка");
        sb.AppendLine();
        sb.AppendLine(
            $"**{review.TotalScore} из {review.MaxScore}** баллов по {review.Criteria.Count} " +
            $"{Plural(review.Criteria.Count, "критерию", "критериям", "критериям")}.");

        if (review.UnverifiedCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"По {review.UnverifiedCount} {Plural(review.UnverifiedCount, "критерию", "критериям", "критериям")} " +
                "подходящий раздел в заявке не найден — это результат автоматической сверки " +
                "оглавления, подтвердите вручную.");
        }

        sb.AppendLine();
        sb.AppendLine("| Критерий | Вывод | Балл |");
        sb.AppendLine("| --- | --- | --- |");

        foreach (var outcome in review.Criteria)
        {
            sb.AppendLine(
                $"| {outcome.Criterion.Index}. {Escape(outcome.Criterion.Name)} " +
                $"| {StatusText(outcome.Status)} | {outcome.Score}/{outcome.Criterion.MaxScore} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Разбор по критериям");

        foreach (var outcome in review.Criteria)
        {
            sb.AppendLine();
            sb.AppendLine($"### {outcome.Criterion.Index}. {outcome.Criterion.Name}");
            sb.AppendLine();
            sb.AppendLine($"{StatusText(outcome.Status)} — {outcome.Score} из {outcome.Criterion.MaxScore}.");

            if (!string.IsNullOrWhiteSpace(outcome.Explanation))
            {
                sb.AppendLine();
                sb.AppendLine(outcome.Explanation.Trim());
            }

            if (outcome.Findings.Count > 0)
            {
                sb.AppendLine();
                foreach (var finding in Ordered(outcome.Findings))
                {
                    sb.AppendLine($"- {Prefix(finding)}{finding.Content}");
                }
            }
            else if (outcome.Status == CriterionStatus.NoIssues)
            {
                sb.AppendLine();
                sb.AppendLine("- Замечаний при проверке не зафиксировано.");
            }
        }

        if (review.Unassigned.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Замечания вне критериев");
            sb.AppendLine();

            foreach (var finding in Ordered(review.Unassigned))
            {
                sb.AppendLine($"- {Prefix(finding)}{finding.Content}");
            }
        }

        if (!string.IsNullOrWhiteSpace(review.Summary))
        {
            sb.AppendLine();
            sb.AppendLine("## Итог");
            sb.AppendLine();
            sb.AppendLine(review.Summary.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    public static string StatusText(CriterionStatus status) => status switch
    {
        CriterionStatus.NoIssues => "Замечаний нет",
        CriterionStatus.Questionable => "Требует уточнения",
        CriterionStatus.Violated => "Нарушение",
        CriterionStatus.NotFound => "Раздел не найден",
        _ => "Не проверялось"
    };

    private static IEnumerable<ReviewFinding> Ordered(IEnumerable<ReviewFinding> findings) =>
        findings
            .OrderByDescending(f => f.Source == FindingSource.Computed)
            .ThenByDescending(f => f.Type == PinType.Mistake)
            .ThenByDescending(f => f.Type == PinType.Attention);

    private static string Prefix(ReviewFinding finding) => finding.Source switch
    {
        FindingSource.Computed => "**Посчитано по документу:** ",
        _ => finding.Type switch
        {
            PinType.Mistake => "**Нарушение:** ",
            PinType.Attention => "**Спорно:** ",
            PinType.WhatToCheck => "**Проверить:** ",
            _ => string.Empty
        }
    };

    private static string Escape(string value) => value.Replace("|", "\\|");

    private static string Plural(int count, string one, string few, string many)
    {
        var mod100 = count % 100;
        if (mod100 is >= 11 and <= 14) return many;

        return (count % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
