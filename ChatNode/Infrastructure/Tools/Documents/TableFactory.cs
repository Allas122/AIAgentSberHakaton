namespace ChatNode.Infrastructure.Tools.Documents;

public static class TableFactory
{
    public static SourceTable? FromRows(string name, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) return null;

        var headerIndex = FindHeader(rows);
        if (headerIndex < 0) return null;

        var header = Deduplicate(rows[headerIndex]);
        if (header.Count == 0) return null;

        var body = rows
            .Skip(headerIndex + 1)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .Select(IReadOnlyList<string> (row) => row.Take(header.Count).ToList())
            .ToList();

        return new SourceTable(name, header, body);
    }

    public static string Normalize(string raw) =>
        raw.Replace(' ', ' ').Replace('\t', ' ').Replace('|', '/').Replace('\n', ' ').Trim();

    private static int FindHeader(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        for (var i = 0; i < rows.Count && i < 10; i++)
        {
            var filled = rows[i].Count(cell => !string.IsNullOrWhiteSpace(cell));

            if (filled >= 2) return i;
        }

        return rows.Count > 0 && rows[0].Any(cell => !string.IsNullOrWhiteSpace(cell)) ? 0 : -1;
    }

    private static List<string> Deduplicate(IReadOnlyList<string> header)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        for (var i = 0; i < header.Count; i++)
        {
            var name = string.IsNullOrWhiteSpace(header[i]) ? $"Колонка {i + 1}" : header[i].Trim();

            if (seen.TryGetValue(name, out var count))
            {
                seen[name] = count + 1;
                name = $"{name} ({count + 1})";
            }
            else
            {
                seen[name] = 1;
            }

            result.Add(name);
        }

        while (result.Count > 0 && result[^1].StartsWith("Колонка ", StringComparison.Ordinal)) result.RemoveAt(result.Count - 1);

        return result;
    }
}
