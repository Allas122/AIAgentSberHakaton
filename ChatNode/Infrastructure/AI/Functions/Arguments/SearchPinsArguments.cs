using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record SearchPinsArguments(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("pin_type")] string? PinType,
    [property: JsonPropertyName("limit")] int Limit);
