using System.Text;
using ChatNode.Infrastructure.Dto;
using ChatNode.Infrastructure.Tools.Abstractions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ChatNode.Infrastructure.Tools;

public class DocxTextExtractor : IDocxTextExtractor
{
    private const int UnknownHeadingLevel = 0;
    private const string DefaultSectionTitle = "Документ";

    private static readonly string[] HeadingStyleMarkers = ["heading", "заголовок", "title", "название"];

    public IReadOnlyList<ApplicationSectionDto> ExtractSections(Stream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null) throw new InvalidOperationException("Документ не содержит тела — файл повреждён или это не docx.");

        var sections = new List<ApplicationSectionDto>();
        var headingStack = new List<string>();
        var content = new StringBuilder();
        var headingPending = false;

        void Flush()
        {
            if (content.Length == 0 && !headingPending) return;

            var title = headingStack.Count > 0 ? string.Join(" > ", headingStack) : DefaultSectionTitle;
            sections.Add(new ApplicationSectionDto(title, content.ToString().TrimEnd()));
            content.Clear();
            headingPending = false;
        }

        void HandleParagraph(Paragraph paragraph)
        {
            var text = Normalize(paragraph.InnerText);
            if (text.Length == 0) return;

            var level = GetHeadingLevel(paragraph, text);
            if (level is null)
            {
                content.AppendLine(text);
                return;
            }

            Flush();

            var depth = level.Value == UnknownHeadingLevel
                ? Math.Max(headingStack.Count - 1, 0)
                : Math.Min(level.Value - 1, headingStack.Count);

            headingStack.RemoveRange(depth, headingStack.Count - depth);
            headingStack.Add(text);
            headingPending = true;
        }

        void Walk(OpenXmlElement container)
        {
            foreach (var element in container.ChildElements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        HandleParagraph(paragraph);
                        break;

                    case Table table:
                        var rendered = RenderTable(table);
                        if (rendered.Length > 0) content.AppendLine(rendered);
                        break;

                    case AlternateContent alternate:
                        var branch = (OpenXmlElement?)alternate.GetFirstChild<AlternateContentChoice>()
                                     ?? alternate.GetFirstChild<AlternateContentFallback>();
                        if (branch is not null) Walk(branch);
                        break;

                    default:
                        if (element.HasChildren) Walk(element);
                        break;
                }
            }
        }

        Walk(body);

        Flush();

        return sections;
    }

    public IReadOnlyList<string> ExtractLines(Stream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null) return [];

        var lines = new List<string>();

        void Walk(OpenXmlElement container)
        {
            foreach (var element in container.ChildElements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        var text = Normalize(paragraph.InnerText);
                        if (text.Length > 0) lines.Add(text);
                        break;

                    case Table table:
                        foreach (var row in Enumerate<TableRow>(table))
                        {
                            var cells = Enumerate<TableCell>(row)
                                .Select(cell => Normalize(cell.InnerText))
                                .Where(cell => cell.Length > 0)
                                .Distinct()
                                .ToList();

                            if (cells.Count > 0) lines.Add(string.Join(" | ", cells));
                        }

                        break;

                    case AlternateContent alternate:
                        var branch = (OpenXmlElement?)alternate.GetFirstChild<AlternateContentChoice>()
                                     ?? alternate.GetFirstChild<AlternateContentFallback>();
                        if (branch is not null) Walk(branch);
                        break;

                    default:
                        if (element.HasChildren) Walk(element);
                        break;
                }
            }
        }

        Walk(body);

        return lines;
    }

    public string ExtractMarkdown(Stream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null) throw new InvalidOperationException("Документ не содержит тела — файл повреждён или это не docx.");

        var markdown = new StringBuilder();

        void Walk(OpenXmlElement container)
        {
            foreach (var element in container.ChildElements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        var text = Normalize(paragraph.InnerText);
                        if (text.Length == 0) break;

                        var level = GetHeadingLevel(paragraph, text);

                        if (level is null)
                        {
                            markdown.AppendLine(text);
                            markdown.AppendLine();
                            break;
                        }

                        var depth = Math.Clamp(level.Value == UnknownHeadingLevel ? 2 : level.Value, 1, 6);

                        markdown.AppendLine($"{new string('#', depth)} {text}");
                        markdown.AppendLine();
                        break;

                    case Table table:
                        var rendered = RenderMarkdownTable(table);
                        if (rendered.Length > 0)
                        {
                            markdown.AppendLine(rendered);
                            markdown.AppendLine();
                        }

                        break;

                    case AlternateContent alternate:
                        var branch = (OpenXmlElement?)alternate.GetFirstChild<AlternateContentChoice>()
                                     ?? alternate.GetFirstChild<AlternateContentFallback>();
                        if (branch is not null) Walk(branch);
                        break;

                    default:
                        if (element.HasChildren) Walk(element);
                        break;
                }
            }
        }

        Walk(body);

        return markdown.ToString().Trim();
    }

    private static string RenderMarkdownTable(Table table)
    {
        var rows = Enumerate<TableRow>(table)
            .Select(row => Enumerate<TableCell>(row).Select(cell => Normalize(cell.InnerText)).ToList())
            .Where(cells => cells.Count > 0 && !cells.All(string.IsNullOrEmpty))
            .ToList();

        if (rows.Count == 0) return string.Empty;

        var width = rows.Max(cells => cells.Count);
        var sb = new StringBuilder();

        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i];
            var padded = Enumerable.Range(0, width)
                .Select(column => column < cells.Count ? cells[column] : string.Empty);

            sb.AppendLine($"| {string.Join(" | ", padded)} |");

            if (i == 0) sb.AppendLine($"|{string.Concat(Enumerable.Repeat(" --- |", width))}");
        }

        return sb.ToString().TrimEnd();
    }

    private static int? GetHeadingLevel(Paragraph paragraph, string text)
    {
        var properties = paragraph.ParagraphProperties;

        var styleId = properties?.ParagraphStyleId?.Val?.Value;
        if (!string.IsNullOrEmpty(styleId) &&
            HeadingStyleMarkers.Any(marker => styleId.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return ParseTrailingDigits(styleId) ?? 1;
        }

        if (properties?.OutlineLevel?.Val?.Value is { } outlineLevel) return outlineLevel + 1;

        if (text.Length > 120 || text.EndsWith('.')) return null;

        var runs = paragraph.Descendants<Run>().ToList();
        if (runs.Count == 0 || !runs.All(run => run.RunProperties?.Bold is not null)) return null;

        return UnknownHeadingLevel;
    }

    private static int? ParseTrailingDigits(string styleId)
    {
        var digits = new string(styleId.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var level) && level > 0 ? level : null;
    }

    private static string RenderTable(Table table)
    {
        var sb = new StringBuilder();

        foreach (var row in Enumerate<TableRow>(table))
        {
            var cells = Enumerate<TableCell>(row)
                .Select(cell => Normalize(cell.InnerText))
                .ToList();

            if (cells.Count == 0 || cells.All(string.IsNullOrEmpty)) continue;

            sb.AppendLine($"| {string.Join(" | ", cells)} |");
        }

        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<T> Enumerate<T>(OpenXmlElement container) where T : OpenXmlElement
    {
        foreach (var element in container.ChildElements)
        {
            if (element is T match)
            {
                yield return match;
                continue;
            }

            if (element is Table or TableRow or Paragraph || !element.HasChildren) continue;

            foreach (var nested in Enumerate<T>(element)) yield return nested;
        }
    }

    private static string Normalize(string raw) =>
        raw.Replace(' ', ' ').Replace('\t', ' ').Trim();
}
