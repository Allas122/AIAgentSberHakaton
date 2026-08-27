using Domain.Entities;

namespace ChatNode.Infrastructure.Analytics;

public static class DatasetQueryEngine
{
    private const int MaxGroups = 50;

    public static async Task<DatasetQueryResult> ExecuteAsync(
        Dataset dataset,
        DatasetQuery query,
        IDatasetQueryRunner runner,
        CancellationToken ct = default)
    {
        var notes = new List<string>();
        var columns = dataset.Columns.Select(column => column.Name).ToList();

        var filters = query.Filters
            .Select(filter => Resolve(filter, columns, notes))
            .Where(filter => filter is not null)
            .Select(filter => filter!)
            .ToList();

        var valueColumn = ResolveColumn(query.ValueColumn, columns, notes, "колонка значения");
        var groupColumn = ResolveColumn(query.GroupBy, columns, notes, "колонка группировки");

        if (query.Aggregate != DatasetAggregate.Count && valueColumn is null)
        {
            notes.Add("Для суммы, среднего, минимума и максимума нужна числовая колонка — вернулось количество строк.");
        }

        var aggregate = query.Aggregate != DatasetAggregate.Count && valueColumn is null
            ? DatasetAggregate.Count
            : query.Aggregate;

        var plan = new DatasetQuery(
            filters,
            aggregate,
            valueColumn,
            groupColumn,
            query.Limit > 0 ? Math.Min(query.Limit, MaxGroups) : MaxGroups);

        var facts = await runner.RunAsync(dataset.Id, plan, ct);

        if (aggregate != DatasetAggregate.Count && valueColumn is not null)
        {
            if (facts.NumericRows == 0)
            {
                notes.Add($"В колонке «{valueColumn}» не нашлось чисел — считать нечего.");
            }
            else if (facts.NumericRows < facts.MatchedRows)
            {
                notes.Add(
                    $"В колонке «{valueColumn}» {facts.MatchedRows - facts.NumericRows} значений не числа — они пропущены.");
            }
        }

        return new DatasetQueryResult(
            facts.Value,
            facts.MatchedRows,
            facts.TotalRows,
            facts.Groups,
            notes.Distinct().ToList());
    }

    private static DatasetFilter? Resolve(DatasetFilter filter, List<string> columns, List<string> notes)
    {
        var column = ResolveColumn(filter.Column, columns, notes, "колонка фильтра");

        return column is null ? null : filter with { Column = column };
    }

    private static string? ResolveColumn(string? requested, List<string> columns, List<string> notes, string role)
    {
        if (string.IsNullOrWhiteSpace(requested)) return null;

        var exact = columns.FirstOrDefault(column => column.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var partial = columns.FirstOrDefault(column =>
            column.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
            requested.Contains(column, StringComparison.OrdinalIgnoreCase));

        if (partial is not null) return partial;

        notes.Add($"Колонки «{requested}» в таблице нет ({role}). Доступные: {string.Join(", ", columns)}.");

        return null;
    }
}
