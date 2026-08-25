using System.Text.RegularExpressions;
using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.AI.Review;

public static class ReviewConsolidator
{
    public const string DiscrepancyMarker = "РАСХОЖДЕНИЯ:";

    private const int DuplicatePrefixChars = 80;
    private const double QuestionableFactor = 0.7;
    private const double ViolatedFactor = 0.3;

    private static readonly string[] BudgetKeywords =
        ["бюджет", "смет", "расход", "финанс", "вклад", "софинанс"];

    public static ApplicationReviewResult Consolidate(
        IReadOnlyList<ReviewCriterion> criteria,
        IReadOnlyList<Pin> pins,
        string numbers,
        string coverage)
    {
        var findings = Distinct(pins).Select(ToFinding).ToList();

        var discrepancies = ParseDiscrepancies(numbers);
        findings.AddRange(discrepancies.Select(text => ToComputedFinding(text, criteria)));

        var notFound = MatchCoverage(coverage, criteria);

        var outcomes = new List<CriterionOutcome>(criteria.Count);

        foreach (var criterion in criteria)
        {
            var own = findings.Where(f => f.CriterionIndex == criterion.Index).ToList();
            var status = Classify(own, notFound.Contains(criterion.Index));

            outcomes.Add(new CriterionOutcome(criterion, status, Score(criterion, status, own), own));
        }

        var unassigned = findings
            .Where(f => f.CriterionIndex is null || criteria.All(c => c.Index != f.CriterionIndex))
            .ToList();

        return new ApplicationReviewResult(
            outcomes,
            unassigned,
            discrepancies,
            outcomes.Sum(o => o.Score),
            outcomes.Sum(o => o.Criterion.MaxScore),
            outcomes.Count(o => o.Status == CriterionStatus.NotFound));
    }

    private static CriterionStatus Classify(IReadOnlyList<ReviewFinding> findings, bool flaggedMissing)
    {
        if (findings.Any(f => f.Type == PinType.Mistake)) return CriterionStatus.Violated;

        if (findings.Any(f => f.Type is PinType.Attention or PinType.WhatToCheck))
            return CriterionStatus.Questionable;

        return flaggedMissing ? CriterionStatus.NotFound : CriterionStatus.NoIssues;
    }

    private static int Score(ReviewCriterion criterion, CriterionStatus status, IReadOnlyList<ReviewFinding> findings)
    {
        var max = criterion.MaxScore;

        return status switch
        {
            CriterionStatus.NoIssues => max,
            CriterionStatus.NotFound => 0,
            CriterionStatus.NotChecked => 0,
            CriterionStatus.Questionable => Clamp(
                (int)Math.Round(max * QuestionableFactor) - Extra(findings, PinType.Attention, PinType.WhatToCheck),
                max / 2,
                max),
            CriterionStatus.Violated => Clamp(
                (int)Math.Round(max * ViolatedFactor) - Extra(findings, PinType.Mistake),
                0,
                max),
            _ => 0
        };
    }

    private static int Extra(IReadOnlyList<ReviewFinding> findings, params PinType[] types) =>
        Math.Max(findings.Count(f => types.Contains(f.Type)) - 1, 0);

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));

    private static ReviewFinding ToFinding(Pin pin) =>
        new(pin.Content, pin.Type, FindingSource.Model, pin.Scope, pin.CriterionIndex);

    private static ReviewFinding ToComputedFinding(string text, IReadOnlyList<ReviewCriterion> criteria)
    {
        var budget = criteria.FirstOrDefault(c =>
            BudgetKeywords.Any(k => c.Name.Contains(k, StringComparison.OrdinalIgnoreCase)));

        return new ReviewFinding(text, PinType.Mistake, FindingSource.Computed, FindingScope.Document, budget?.Index);
    }

    public static IReadOnlyList<string> ParseDiscrepancies(string numbers)
    {
        if (string.IsNullOrWhiteSpace(numbers)) return [];

        var start = numbers.IndexOf(DiscrepancyMarker, StringComparison.Ordinal);
        if (start < 0) return [];

        return numbers[(start + DiscrepancyMarker.Length)..]
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '•', '*', ' '))
            .Where(line => line.Length > 0)
            .ToList();
    }

    public static HashSet<int> MatchCoverage(string coverage, IReadOnlyList<ReviewCriterion> criteria)
    {
        var missing = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(coverage)) return missing;

        foreach (var line in coverage.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = Normalize(line.Split('—', '–', '-')[0]);
            if (name.Length < 4) continue;

            var match = criteria.FirstOrDefault(c =>
            {
                var normalized = Normalize(c.Name);
                return normalized.Length >= 4 && (normalized == name || name.Contains(normalized));
            });

            if (match is not null) missing.Add(match.Index);
        }

        return missing;
    }

    private static string Normalize(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static List<Pin> Distinct(IReadOnlyList<Pin> pins)
    {
        var kept = new List<Pin>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pin in pins)
        {
            var key = Regex.Replace(pin.Content, @"\s+", " ").Trim();
            key = key.Length <= DuplicatePrefixChars ? key : key[..DuplicatePrefixChars];

            if (seen.Add($"{pin.Type}|{key}")) kept.Add(pin);
        }

        return kept;
    }
}
