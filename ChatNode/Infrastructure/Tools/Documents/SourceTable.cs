using System.Text;

namespace ChatNode.Infrastructure.Tools.Documents;

public record SourceTable(string Name, IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows);

public static class TableMarkdown
{
    public const int PreviewRows = 50;

    public static string Render(SourceTable table)
    {
        if (table.Header.Count == 0) return string.Empty;

        var width = table.Header.Count;
        var sb = new StringBuilder();

        sb.AppendLine($"| {string.Join(" | ", table.Header)} |");
        sb.AppendLine($"|{string.Concat(Enumerable.Repeat(" --- |", width))}");

        foreach (var row in table.Rows.Take(PreviewRows))
        {
            var padded = Enumerable.Range(0, width)
                .Select(column => column < row.Count ? row[column] : string.Empty);

            sb.AppendLine($"| {string.Join(" | ", padded)} |");
        }

        if (table.Rows.Count > PreviewRows)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"Показаны первые {PreviewRows} строк из {table.Rows.Count}. " +
                "Таблица целиком загружена в базу данных — точные подсчёты по ней делает query_dataset, " +
                "а не чтение этого фрагмента.");
        }

        return sb.ToString().TrimEnd();
    }

    public static string Render(IReadOnlyList<SourceTable> tables)
    {
        var markdown = new StringBuilder();

        foreach (var table in tables)
        {
            var rendered = Render(table);
            if (rendered.Length == 0) continue;

            if (!string.IsNullOrWhiteSpace(table.Name))
            {
                markdown.AppendLine($"## {table.Name}");
                markdown.AppendLine();
            }

            markdown.AppendLine(rendered);
            markdown.AppendLine();
        }

        return markdown.ToString().Trim();
    }
}
