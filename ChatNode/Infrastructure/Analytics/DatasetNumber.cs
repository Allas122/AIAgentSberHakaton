using System.Globalization;

namespace ChatNode.Infrastructure.Analytics;

public static class DatasetNumber
{
    public const char NonBreakingSpace = '\u00A0';

    public const char NarrowNonBreakingSpace = '\u202F';

    private static readonly char[] Noise = [' ', NonBreakingSpace, NarrowNonBreakingSpace, '%'];

    public static double? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var cleaned = new string(raw.Where(symbol => !Noise.Contains(symbol)).ToArray())
            .Replace(',', '.')
            .Trim();

        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }
}
