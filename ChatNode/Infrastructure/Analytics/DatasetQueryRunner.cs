using System.Data;
using System.Data.Common;
using ChatNode.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ChatNode.Infrastructure.Analytics;

public interface IDatasetQueryRunner
{
    Task<DatasetQueryFacts> RunAsync(Guid datasetId, DatasetQuery plan, CancellationToken ct = default);
}

public class DatasetQueryRunner(AppDbContext context) : IDatasetQueryRunner
{
    private const string EmptyGroup = "(пусто)";

    private const string NumberPattern = "^[+-]?([0-9]+([.][0-9]*)?|[.][0-9]+)([eE][+-]?[0-9]+)?$";

    public async Task<DatasetQueryFacts> RunAsync(Guid datasetId, DatasetQuery plan, CancellationToken ct = default)
    {
        await context.Database.OpenConnectionAsync(ct);

        try
        {
            var connection = context.Database.GetDbConnection();

            var totals = await TotalsAsync(connection, datasetId, plan, ct);

            var groups = plan.GroupBy is null
                ? []
                : await GroupsAsync(connection, datasetId, plan, ct);

            return totals with { Groups = groups };
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<DatasetQueryFacts> TotalsAsync(
        DbConnection connection,
        Guid datasetId,
        DatasetQuery plan,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();

        var binder = new ParameterBinder(command);

        command.CommandText = $"""
            WITH source AS (
                SELECT "Values" AS v FROM dataset_rows WHERE "DatasetId" = {binder.Uuid(datasetId)}
            ),
            matched AS (
                SELECT v FROM source WHERE {Where(binder, plan.Filters)}
            ),
            measured AS (
                SELECT {Measure(binder, plan.ValueColumn)} AS n FROM matched
            )
            SELECT
                (SELECT count(*) FROM source)::int,
                (SELECT count(*) FROM matched)::int,
                (SELECT count(n) FROM measured)::int,
                (SELECT {Aggregation(plan.Aggregate)} FROM measured)
            """;

        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return new DatasetQueryFacts(0, 0, 0, 0, []);

        return new DatasetQueryFacts(
            reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
            reader.GetInt32(1),
            reader.GetInt32(0),
            reader.GetInt32(2),
            []);
    }

    private static async Task<IReadOnlyList<DatasetGroupResult>> GroupsAsync(
        DbConnection connection,
        Guid datasetId,
        DatasetQuery plan,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();

        var binder = new ParameterBinder(command);

        command.CommandText = $"""
            WITH matched AS (
                SELECT "Values" AS v FROM dataset_rows
                WHERE "DatasetId" = {binder.Uuid(datasetId)} AND {Where(binder, plan.Filters)}
            ),
            measured AS (
                SELECT
                    coalesce(nullif(v ->> {binder.Text(plan.GroupBy!)}, ''), {binder.Text(EmptyGroup)}) AS grp,
                    {Measure(binder, plan.ValueColumn)} AS n
                FROM matched
            )
            SELECT grp, {Aggregation(plan.Aggregate)}, count(*)::int
            FROM measured
            GROUP BY grp
            ORDER BY 2 DESC
            LIMIT {binder.Integer(plan.Limit)}
            """;

        await using var reader = await command.ExecuteReaderAsync(ct);

        var groups = new List<DatasetGroupResult>();

        while (await reader.ReadAsync(ct))
        {
            groups.Add(new DatasetGroupResult(
                reader.GetString(0),
                reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                reader.GetInt32(2)));
        }

        return groups;
    }

    private static string Where(ParameterBinder binder, IReadOnlyList<DatasetFilter> filters) =>
        filters.Count == 0
            ? "TRUE"
            : string.Join(" AND ", filters.Select(filter => Predicate(binder, filter)));

    private static string Predicate(ParameterBinder binder, DatasetFilter filter)
    {
        var cell = Cell(binder.Text(filter.Column));
        var value = (filter.Value ?? string.Empty).Trim();

        return filter.Operator switch
        {
            DatasetFilterOperator.Empty => $"{cell} ~ '^[[:space:]]*$'",
            DatasetFilterOperator.NotEmpty => $"{cell} !~ '^[[:space:]]*$'",
            DatasetFilterOperator.Equals => $"lower(btrim({cell})) = lower({binder.Text(value)})",
            DatasetFilterOperator.NotEquals => $"lower(btrim({cell})) <> lower({binder.Text(value)})",
            DatasetFilterOperator.Contains => $"strpos(lower({cell}), lower({binder.Text(value)})) > 0",
            _ => Comparison(binder, filter, cell)
        };
    }

    private static string Comparison(ParameterBinder binder, DatasetFilter filter, string cell)
    {
        var op = filter.Operator switch
        {
            DatasetFilterOperator.Greater => ">",
            DatasetFilterOperator.GreaterOrEqual => ">=",
            DatasetFilterOperator.Less => "<",
            DatasetFilterOperator.LessOrEqual => "<=",
            _ => null
        };

        if (op is null || DatasetNumber.Parse(filter.Value) is not { } number) return "FALSE";

        return $"{Numeric(cell)} {op} {binder.Number(number)}";
    }

    private static string Measure(ParameterBinder binder, string? valueColumn) =>
        valueColumn is null
            ? "NULL::double precision"
            : Numeric(Cell(binder.Text(valueColumn)));

    private static string Aggregation(DatasetAggregate aggregate) => aggregate switch
    {
        DatasetAggregate.Sum => "coalesce(sum(n), 0)",
        DatasetAggregate.Average => "coalesce(round(avg(n)::numeric, 2), 0)::double precision",
        DatasetAggregate.Min => "coalesce(min(n), 0)",
        DatasetAggregate.Max => "coalesce(max(n), 0)",
        _ => "count(*)::double precision"
    };

    private static string Cell(string columnParameter) => $"coalesce(v ->> {columnParameter}, '')";

    private static readonly int[] NoiseCodes =
    [
        ' ',
        DatasetNumber.NonBreakingSpace,
        DatasetNumber.NarrowNonBreakingSpace,
        '%'
    ];

    private static string Numeric(string cell) =>
        $"CASE WHEN {Cleaned(cell)} ~ '{NumberPattern}' THEN ({Cleaned(cell)})::double precision END";

    private static string Cleaned(string cell)
    {
        var expression = NoiseCodes.Aggregate(cell, (text, code) => $"replace({text}, chr({code}), '')");

        return $"replace({expression}, ',', '.')";
    }

    private sealed class ParameterBinder(DbCommand command)
    {
        public string Uuid(Guid value) => Add(value, DbType.Guid);

        public string Text(string value) => Add(value, DbType.String);

        public string Number(double value) => Add(value, DbType.Double);

        public string Integer(int value) => Add(value, DbType.Int32);

        private string Add(object value, DbType type)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = $"p{command.Parameters.Count}";
            parameter.DbType = type;
            parameter.Value = value;

            command.Parameters.Add(parameter);

            return $"@{parameter.ParameterName}";
        }
    }
}
