using ChatNode.Infrastructure.Manuals;
using ChatNode.Infrastructure.Tools.Documents;
using Domain.Entities;
using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Analytics;

public static class DatasetFactory
{
    private const int MaxRows = 50_000;
    private const double NumericShare = 0.8;

    public static Dataset Create(ManualJob job, SourceTable table)
    {
        var rows = table.Rows.Take(MaxRows).ToList();

        var dataset = new Dataset
        {
            Id = Guid.NewGuid(),
            ManualId = job.ManualId,
            OwnerId = job.OwnerId,
            Title = string.IsNullOrWhiteSpace(table.Name) ? job.Title : $"{job.Title} — {table.Name}",
            SheetName = table.Name,
            RowCount = rows.Count,
            CreatedAt = DateTimeOffset.UtcNow
        };

        for (var i = 0; i < table.Header.Count; i++)
        {
            dataset.Columns.Add(new DatasetColumn
            {
                Id = Guid.NewGuid(),
                DatasetId = dataset.Id,
                Name = table.Header[i],
                Ordinal = i,
                Kind = DetectKind(rows, i)
            });
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var values = new Dictionary<string, string>(table.Header.Count, StringComparer.OrdinalIgnoreCase);

            for (var column = 0; column < table.Header.Count; column++)
            {
                values[table.Header[column]] = column < rows[i].Count ? rows[i][column] : string.Empty;
            }

            dataset.Rows.Add(new DatasetRow
            {
                Id = Guid.NewGuid(),
                DatasetId = dataset.Id,
                Ordinal = i,
                Values = values
            });
        }

        return dataset;
    }

    private static DatasetColumnKind DetectKind(IReadOnlyList<IReadOnlyList<string>> rows, int column)
    {
        var filled = rows
            .Select(row => column < row.Count ? row[column] : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (filled.Count == 0) return DatasetColumnKind.Text;

        if (filled.Count(value => DatasetNumber.Parse(value) is not null) >= filled.Count * NumericShare)
        {
            return DatasetColumnKind.Number;
        }

        return filled.Count(IsDate) >= filled.Count * NumericShare
            ? DatasetColumnKind.Date
            : DatasetColumnKind.Text;
    }

    private static bool IsDate(string value) => DateTime.TryParse(value, out _);
}
