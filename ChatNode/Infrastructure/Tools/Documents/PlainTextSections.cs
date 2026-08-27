using System.Text;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.Dto;

namespace ChatNode.Infrastructure.Tools.Documents;

public static partial class PlainTextSections
{
    private const string DefaultTitle = "Начало документа";
    private const int MaxHeadingChars = 120;
    private const int MinHeadingChars = 3;

    [GeneratedRegex(@"^\d+(\.\d+)*[.)]?\s+\S", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex NumberedHeading { get; }

    [GeneratedRegex(
        @"^(раздел|глава|часть|приложение|пункт)\s+[\dIVXLC]",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex NamedHeading { get; }

    public static IReadOnlyList<ApplicationSectionDto> Parse(IReadOnlyList<string> lines)
    {
        var sections = new List<ApplicationSectionDto>();
        var content = new StringBuilder();
        var title = DefaultTitle;

        void Flush()
        {
            if (content.Length == 0) return;

            sections.Add(new ApplicationSectionDto(title, content.ToString().TrimEnd()));
            content.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0) continue;

            if (IsHeading(line))
            {
                Flush();
                title = line;
                continue;
            }

            content.AppendLine(line);
        }

        Flush();

        if (sections.Count == 0 && lines.Any(line => line.Trim().Length > 0))
        {
            sections.Add(new ApplicationSectionDto(
                DefaultTitle,
                string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0))));
        }

        return sections;
    }

    private static bool IsHeading(string line)
    {
        if (line.Length is < MinHeadingChars or > MaxHeadingChars) return false;
        if (line.EndsWith('.') || line.EndsWith(',') || line.EndsWith(';')) return false;
        if (!line.Any(char.IsLetter)) return false;

        if (NumberedHeading.IsMatch(line) || NamedHeading.IsMatch(line)) return true;

        return line.Where(char.IsLetter).All(char.IsUpper);
    }
}
