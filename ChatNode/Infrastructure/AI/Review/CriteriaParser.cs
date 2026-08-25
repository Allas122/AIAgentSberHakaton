using System.Text.RegularExpressions;
using Domain.Entities;

namespace ChatNode.Infrastructure.AI.Review;

public static class CriteriaParser
{
    public const int DefaultMaxScore = 10;
    private const int MaxCriteria = 30;
    private const int MaxNameChars = 200;

    private static readonly Regex LeadingNumber = new(@"^\s*\d+\s*[.)]?\s*", RegexOptions.Compiled);
    private static readonly Regex TrailingScore = new(@"[-–—]\s*(\d{1,3})\s*балл\w*\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<ReviewCriterion> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        if (raw.TrimStart().StartsWith("НЕТ", StringComparison.OrdinalIgnoreCase)) return [];

        var lines = raw
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("ШКАЛА", StringComparison.OrdinalIgnoreCase));

        var result = new List<ReviewCriterion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var criterion = ParseLine(line, result.Count + 1);
            if (criterion is null) continue;
            if (!seen.Add(Normalize(criterion.Name))) continue;

            result.Add(criterion with { Index = result.Count + 1 });

            if (result.Count >= MaxCriteria) break;
        }

        return result;
    }

    private static ReviewCriterion? ParseLine(string line, int fallbackIndex)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);

        if (parts.Length >= 3)
        {
            var name = Clean(parts[1]);
            if (name.Length == 0) return null;

            var score = int.TryParse(parts[2], out var parsed) && parsed is > 0 and <= 100
                ? parsed
                : DefaultMaxScore;

            return new ReviewCriterion(fallbackIndex, name, score);
        }

        var fallback = Clean(LeadingNumber.Replace(line, string.Empty).Split('—', '–', '-')[0]);
        if (fallback.Length < 3) return null;

        var trailing = TrailingScore.Match(line);
        var maxScore = trailing.Success && int.TryParse(trailing.Groups[1].Value, out var fromText)
            ? fromText
            : DefaultMaxScore;

        return new ReviewCriterion(fallbackIndex, fallback, maxScore);
    }

    private static string Clean(string value)
    {
        var cleaned = value.Trim().Trim('*', '•', '.', ':', ' ', '"', '«', '»');
        return cleaned.Length <= MaxNameChars ? cleaned : cleaned[..MaxNameChars];
    }

    private static string Normalize(string name) =>
        new(name.Where(char.IsLetterOrDigit).ToArray());
}
