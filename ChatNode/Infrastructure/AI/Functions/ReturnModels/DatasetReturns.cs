namespace ChatNode.Infrastructure.AI.Functions.ReturnModels;

public record DatasetColumnView(string Name, string Kind, IReadOnlyList<string> Examples);

public record DatasetView(
    string Id,
    string Title,
    string Sheet,
    int Rows,
    IReadOnlyList<DatasetColumnView> Columns);

public record ListDatasetsReturn(int Total, IReadOnlyList<DatasetView> Datasets, string Hint);

public record DatasetErrorReturn(string Status, string Message);

public record DatasetGroupView(string Group, double Value, int Rows);

public record QueryDatasetReturn(
    string Dataset,
    string Aggregate,
    double Value,
    int MatchedRows,
    int TotalRows,
    IReadOnlyList<DatasetGroupView> Groups,
    IReadOnlyList<string> Notes,
    string Explanation);
