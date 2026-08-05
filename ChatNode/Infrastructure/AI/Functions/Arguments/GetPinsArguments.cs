using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record GetPinsArguments(
    [property: JsonPropertyName("pin_type")] string? PinType);
