using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record CreatePinArguments(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("pin_type")] string PinType);
