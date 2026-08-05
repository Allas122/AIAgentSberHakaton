using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record UpdatePinArguments(
    [property: JsonPropertyName("pin_id")] string PinId,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("pin_type")] string? PinType);
