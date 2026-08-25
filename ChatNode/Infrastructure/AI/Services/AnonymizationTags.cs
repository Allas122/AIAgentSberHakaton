using System.Text.RegularExpressions;

namespace ChatNode.Infrastructure.AI.Services;

public static class AnonymizationTags
{
    public static readonly Regex Pattern = new(
        @"\[([\p{L}][\p{L}_ ]*)_([0-9A-F]+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool Contains(string? text) =>
        !string.IsNullOrEmpty(text) && Pattern.IsMatch(text);
}
