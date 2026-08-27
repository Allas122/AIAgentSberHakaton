using Domain.ValueTypes;

namespace Domain.Entities;

public class Dataset
{
    public Guid Id { get; set; }

    public Guid ManualId { get; set; }

    public Guid OwnerId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SheetName { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<DatasetColumn> Columns { get; set; } = [];

    public List<DatasetRow> Rows { get; set; } = [];
}

public class DatasetColumn
{
    public Guid Id { get; set; }

    public Guid DatasetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public DatasetColumnKind Kind { get; set; }
}

public class DatasetRow
{
    public Guid Id { get; set; }

    public Guid DatasetId { get; set; }

    public int Ordinal { get; set; }

    public Dictionary<string, string> Values { get; set; } = [];
}
