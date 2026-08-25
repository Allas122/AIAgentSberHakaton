using System.Text.RegularExpressions;

namespace ChatNode.Infrastructure.Tools.Documents;

public static class MarkdownDocumentParser
{
    private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex NumberedPattern = new(@"^\s*\d+[.)]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex TableSeparator = new(@"^\s*\|?\s*:?-{2,}", RegexOptions.Compiled);
    private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex ItalicPattern = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);

    public static IReadOnlyList<DocumentBlock> Parse(string markdown)
    {
        var blocks = new List<DocumentBlock>();
        if (string.IsNullOrWhiteSpace(markdown)) return blocks;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var bullets = new List<string>();
        var numbered = false;
        var paragraph = new List<string>();
        var tableRows = new List<IReadOnlyList<string>>();

        void FlushBullets()
        {
            if (bullets.Count == 0) return;
            blocks.Add(new BulletsBlock([..bullets], numbered));
            bullets.Clear();
        }

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            blocks.Add(new ParagraphBlock(string.Join(" ", paragraph)));
            paragraph.Clear();
        }

        void FlushTable()
        {
            if (tableRows.Count == 0) return;

            var header = tableRows[0];
            var rows = tableRows.Skip(1).ToList();
            blocks.Add(new TableBlock(header, rows));
            tableRows.Clear();
        }

        void FlushAll()
        {
            FlushBullets();
            FlushParagraph();
            FlushTable();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushAll();
                continue;
            }

            if (IsTableRow(line))
            {
                FlushBullets();
                FlushParagraph();

                if (TableSeparator.IsMatch(line)) continue;

                tableRows.Add(SplitTableRow(line));
                continue;
            }

            FlushTable();

            var heading = HeadingPattern.Match(line);
            if (heading.Success)
            {
                FlushAll();
                var text = Inline(heading.Groups[2].Value);
                if (text.Length > 0) blocks.Add(new HeadingBlock(heading.Groups[1].Value.Length, text));
                continue;
            }

            var bullet = BulletPattern.Match(line);
            if (bullet.Success)
            {
                FlushParagraph();
                if (numbered && bullets.Count > 0) FlushBullets();
                numbered = false;
                bullets.Add(Inline(bullet.Groups[1].Value));
                continue;
            }

            var numberedItem = NumberedPattern.Match(line);
            if (numberedItem.Success)
            {
                FlushParagraph();
                if (!numbered && bullets.Count > 0) FlushBullets();
                numbered = true;
                bullets.Add(Inline(numberedItem.Groups[1].Value));
                continue;
            }

            if (IsHorizontalRule(line))
            {
                FlushAll();
                continue;
            }

            FlushBullets();
            paragraph.Add(Inline(line.Trim()));
        }

        FlushAll();

        return blocks;
    }

    private static bool IsTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.Count(c => c == '|') >= 2;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && (trimmed.All(c => c == '-') || trimmed.All(c => c == '*') || trimmed.All(c => c == '_'));
    }

    private static IReadOnlyList<string> SplitTableRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(cell => Inline(cell.Trim())).ToList();
    }

    private static string Inline(string text)
    {
        var result = LinkPattern.Replace(text, "$1");
        result = BoldPattern.Replace(result, "$1");
        result = ItalicPattern.Replace(result, "$1");
        result = CodePattern.Replace(result, "$1");

        return result.Trim();
    }
}
