namespace ChatNode.Infrastructure.Analytics;

public enum DatasetFilterOperator
{
    Equals,
    NotEquals,
    Contains,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    Empty,
    NotEmpty
}

public enum DatasetAggregate
{
    Count,
    Sum,
    Average,
    Min,
    Max
}

public record DatasetFilter(string Column, DatasetFilterOperator Operator, string? Value);

public record DatasetQuery(
    IReadOnlyList<DatasetFilter> Filters,
    DatasetAggregate Aggregate,
    string? ValueColumn,
    string? GroupBy,
    int Limit);

public record DatasetGroupResult(string Group, double Value, int Rows);

public record DatasetQueryResult(
    double Value,
    int MatchedRows,
    int TotalRows,
    IReadOnlyList<DatasetGroupResult> Groups,
    IReadOnlyList<string> Notes);

public record DatasetQueryFacts(
    double Value,
    int MatchedRows,
    int TotalRows,
    int NumericRows,
    IReadOnlyList<DatasetGroupResult> Groups);
