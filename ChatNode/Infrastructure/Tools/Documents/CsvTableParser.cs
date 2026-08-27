using System.Text;

namespace ChatNode.Infrastructure.Tools.Documents;

public static class CsvTableParser
{
    private static readonly char[] Delimiters = [';', ',', '\t'];

    public static SourceTable? Parse(string csv, string name)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;

        var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0) return null;

        var delimiter = DetectDelimiter(lines[0]);

        var rows = lines
            .Select(IReadOnlyList<string> (line) => SplitRow(line, delimiter))
            .ToList();

        return TableFactory.FromRows(name, rows);
    }

    private static char DetectDelimiter(string header) =>
        Delimiters
            .Select(delimiter => (Delimiter: delimiter, Count: SplitRow(header, delimiter).Count))
            .OrderByDescending(candidate => candidate.Count)
            .First()
            .Delimiter;

    private static List<string> SplitRow(string line, char delimiter)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var symbol = line[i];

            if (quoted)
            {
                if (symbol != '"')
                {
                    cell.Append(symbol);
                    continue;
                }

                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                    continue;
                }

                quoted = false;
                continue;
            }

            if (symbol == '"')
            {
                quoted = true;
                continue;
            }

            if (symbol == delimiter)
            {
                cells.Add(TableFactory.Normalize(cell.ToString()));
                cell.Clear();
                continue;
            }

            cell.Append(symbol);
        }

        cells.Add(TableFactory.Normalize(cell.ToString()));

        return cells;
    }
}
