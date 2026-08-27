using System.Globalization;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.Analytics;
using Domain.Entities;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class DatasetFunctionToolsSet(
    IDatasetRepository datasetRepository,
    IDatasetQueryRunner queryRunner,
    ManualScope? scope,
    ILogger<DatasetFunctionToolsSet> logger) : IFunctionToolsSet
{
    private const int ExampleCount = 3;
    private const int ExampleChars = 60;
    private const int SampleRows = 20;
    private const int MaxDatasets = 20;
    private const int MaxColumnsShown = 40;
    private const int MaxGroupNameChars = 120;
    private const int MaxNotes = 5;
    private const int DefaultLimit = 20;

    private static readonly string[] AggregateValues = ["count", "sum", "average", "min", "max"];

    private readonly RepeatCallGuard _guard = new();

    public IReadOnlyList<IChatFunctionTool> FunctionTools =>
    [
        FunctionTool.Create<ListDatasetsArguments>(
            name: "list_datasets",
            description:
                "Показать таблицы, загруженные в базу знаний: мониторинги, выгрузки, реестры. " +
                "Возвращает название каждой таблицы, число строк, список колонок и примеры значений. " +
                "Вызывай ПЕРВЫМ, прежде чем считать что-либо по данным: без точных названий колонок " +
                "запрос не построить.",
            handler: ListDatasets,
            parameters: FunctionParameter.Parameters(new Dictionary<string, FunctionParametersProperty>())
        ),
        FunctionTool.Create<QueryDatasetArguments>(
            name: "query_dataset",
            description:
                "Посчитать по таблице: сколько строк подходит под условия, сумма, среднее, минимум, максимум. " +
                "Считает код, не ты — полученное число переноси в ответ дословно и не пересчитывай. " +
                "Так отвечай на вопросы вида «сколько у нас очников-сирот», «какая общая сумма», " +
                "«сколько человек по каждому факультету». Сам по строкам таблицы не считай никогда.",
            handler: QueryDataset,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["dataset"] = FunctionParameter.String(
                        "Название или идентификатор таблицы из list_datasets. " +
                        "Если таблица одна, можно не указывать."),
                    ["filters"] = FunctionParameter.String(
                        "Условия отбора через точку с запятой: «Форма обучения=Очная; Категория~сирот». " +
                        "Операторы: = равно, != не равно, ~ содержит, > < >= <= для чисел. " +
                        "Названия колонок бери из list_datasets."),
                    ["aggregate"] = FunctionParameter.String(
                        "Что посчитать: count — число строк (по умолчанию), sum, average, min, max.",
                        AggregateValues),
                    ["value_column"] = FunctionParameter.String(
                        "Числовая колонка для sum, average, min и max. Для count не нужна."),
                    ["group_by"] = FunctionParameter.String(
                        "Колонка для разбивки результата по группам: факультет, курс, категория."),
                    ["limit"] = FunctionParameter.Integer(
                        "Сколько групп вернуть при group_by. По умолчанию 20.")
                },
                required: []
            )
        )
    ];

    public async Task<string> ListDatasets(ListDatasetsArguments _)
    {
        if (_guard.IsRepeat("list_datasets")) return RepeatCallGuard.RepeatResponse("list_datasets");

        var datasets = await datasetRepository.ListAsync(scope);

        var views = new List<DatasetView>();

        foreach (var dataset in datasets.Take(MaxDatasets))
        {
            var sample = await datasetRepository.SampleRowsAsync(dataset.Id, SampleRows);

            var columns = dataset.Columns.OrderBy(column => column.Ordinal).ToList();

            views.Add(new DatasetView(
                dataset.Id.ToString(),
                dataset.Title,
                dataset.SheetName,
                dataset.RowCount,
                columns
                    .Take(MaxColumnsShown)
                    .Select(column => new DatasetColumnView(
                        column.Name,
                        column.Kind.ToString(),
                        Examples(sample, column.Name)))
                    .ToList()));

            if (columns.Count > MaxColumnsShown)
            {
                logger.LogDebug(
                    "Таблица {Dataset}: показано {Shown} колонок из {Total}",
                    dataset.Title,
                    MaxColumnsShown,
                    columns.Count);
            }
        }

        logger.LogDebug("Список таблиц базы знаний: {Count} из {Total}", views.Count, datasets.Count);

        var hint = views.Count == 0
            ? "Таблиц пока нет. Их создаёт загрузка .csv или .xlsx в базу знаний."
            : datasets.Count > MaxDatasets
                ? $"Показаны {MaxDatasets} последних таблиц из {datasets.Count}. " +
                  "Для подсчёта вызывай query_dataset — считает код, а не ты."
                : "Для подсчёта вызывай query_dataset — считает код, а не ты.";

        return ToolJson.Serialize(new ListDatasetsReturn(datasets.Count, views, hint));
    }

    public async Task<string> QueryDataset(QueryDatasetArguments arguments)
    {
        if (_guard.IsRepeat(
                "query_dataset",
                arguments.Dataset,
                arguments.Filters,
                arguments.Aggregate,
                arguments.ValueColumn,
                arguments.GroupBy))
        {
            return RepeatCallGuard.RepeatResponse("query_dataset");
        }

        var datasets = await datasetRepository.ListAsync(scope);

        if (datasets.Count == 0)
        {
            return ToolJson.Serialize(new DatasetErrorReturn(
                "Error",
                "В базе знаний нет ни одной таблицы. Загрузите мониторинг в формате .csv или .xlsx."));
        }

        var dataset = Resolve(datasets, arguments.Dataset);

        if (dataset is null)
        {
            return ToolJson.Serialize(new DatasetErrorReturn(
                "Error",
                $"Таблицы «{arguments.Dataset}» нет. Доступные: {string.Join(", ", datasets.Select(item => item.Title))}."));
        }

        var query = new DatasetQuery(
            ParseFilters(arguments.Filters),
            ParseAggregate(arguments.Aggregate),
            arguments.ValueColumn,
            arguments.GroupBy,
            arguments.Limit ?? DefaultLimit);

        var result = await DatasetQueryEngine.ExecuteAsync(dataset, query, queryRunner);

        logger.LogDebug(
            "Запрос к таблице {Dataset}: фильтров={Filters}, агрегат={Aggregate}, подошло {Matched} из {Total}",
            dataset.Title,
            query.Filters.Count,
            query.Aggregate,
            result.MatchedRows,
            result.TotalRows);

        return ToolJson.Serialize(new QueryDatasetReturn(
            dataset.Title,
            query.Aggregate.ToString().ToLowerInvariant(),
            result.Value,
            result.MatchedRows,
            result.TotalRows,
            result.Groups
                .Select(group => new DatasetGroupView(
                    Shorten(group.Group, MaxGroupNameChars),
                    group.Value,
                    group.Rows))
                .ToList(),
            result.Notes.Take(MaxNotes).ToList(),
            Explain(dataset, query, result)));
    }

    private static string Explain(Dataset dataset, DatasetQuery query, DatasetQueryResult result)
    {
        var value = result.Value.ToString("0.##", CultureInfo.InvariantCulture);

        var what = query.Aggregate switch
        {
            DatasetAggregate.Count => $"строк, подходящих под условия: {value}",
            DatasetAggregate.Sum => $"сумма по колонке «{query.ValueColumn}»: {value}",
            DatasetAggregate.Average => $"среднее по колонке «{query.ValueColumn}»: {value}",
            DatasetAggregate.Min => $"минимум по колонке «{query.ValueColumn}»: {value}",
            DatasetAggregate.Max => $"максимум по колонке «{query.ValueColumn}»: {value}",
            _ => value
        };

        return $"Посчитано кодом по таблице «{dataset.Title}» ({result.MatchedRows} из {result.TotalRows} строк): {what}.";
    }

    private static Dataset? Resolve(IReadOnlyList<Dataset> datasets, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return datasets.Count == 1 ? datasets[0] : null;

        var trimmed = requested.Trim();

        if (Guid.TryParse(trimmed, out var id))
        {
            return datasets.FirstOrDefault(dataset => dataset.Id == id);
        }

        return datasets.FirstOrDefault(dataset => dataset.Title.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
               ?? datasets.FirstOrDefault(dataset =>
                   dataset.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                   dataset.SheetName.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> Examples(IReadOnlyList<DatasetRow> rows, string column) =>
        rows.Select(row => row.Values.GetValueOrDefault(column) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(ExampleCount)
            .Select(value => Shorten(value, ExampleChars))
            .ToList();

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static DatasetAggregate ParseAggregate(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sum" => DatasetAggregate.Sum,
            "average" or "avg" or "mean" => DatasetAggregate.Average,
            "min" => DatasetAggregate.Min,
            "max" => DatasetAggregate.Max,
            _ => DatasetAggregate.Count
        };

    private static IReadOnlyList<DatasetFilter> ParseFilters(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var filters = new List<DatasetFilter>();

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Split(part, ">=") is { } greaterOrEqual)
            {
                filters.Add(new DatasetFilter(greaterOrEqual.Left, DatasetFilterOperator.GreaterOrEqual, greaterOrEqual.Right));
                continue;
            }

            if (Split(part, "<=") is { } lessOrEqual)
            {
                filters.Add(new DatasetFilter(lessOrEqual.Left, DatasetFilterOperator.LessOrEqual, lessOrEqual.Right));
                continue;
            }

            if (Split(part, "!=") is { } notEquals)
            {
                filters.Add(new DatasetFilter(notEquals.Left, DatasetFilterOperator.NotEquals, notEquals.Right));
                continue;
            }

            if (Split(part, "~") is { } contains)
            {
                filters.Add(new DatasetFilter(contains.Left, DatasetFilterOperator.Contains, contains.Right));
                continue;
            }

            if (Split(part, ">") is { } greater)
            {
                filters.Add(new DatasetFilter(greater.Left, DatasetFilterOperator.Greater, greater.Right));
                continue;
            }

            if (Split(part, "<") is { } less)
            {
                filters.Add(new DatasetFilter(less.Left, DatasetFilterOperator.Less, less.Right));
                continue;
            }

            if (Split(part, "=") is { } equals)
            {
                filters.Add(equals.Right.Length == 0
                    ? new DatasetFilter(equals.Left, DatasetFilterOperator.Empty, null)
                    : new DatasetFilter(equals.Left, DatasetFilterOperator.Equals, equals.Right));
            }
        }

        return filters;
    }

    private static (string Left, string Right)? Split(string part, string separator)
    {
        var index = part.IndexOf(separator, StringComparison.Ordinal);

        if (index <= 0) return null;

        return (part[..index].Trim(), part[(index + separator.Length)..].Trim());
    }
}
