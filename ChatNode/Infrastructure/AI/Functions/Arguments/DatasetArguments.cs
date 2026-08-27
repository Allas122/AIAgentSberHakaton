using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record ListDatasetsArguments;

public record QueryDatasetArguments(
    [property: JsonPropertyName("dataset")] string? Dataset,
    [property: JsonPropertyName("filters")] string? Filters,
    [property: JsonPropertyName("aggregate")] string? Aggregate,
    [property: JsonPropertyName("value_column")] string? ValueColumn,
    [property: JsonPropertyName("group_by")] string? GroupBy,
    [property: JsonPropertyName("limit")] int? Limit);
