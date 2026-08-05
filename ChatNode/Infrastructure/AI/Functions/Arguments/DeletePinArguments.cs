using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record DeletePinArguments(
    [property: JsonPropertyName("pin_id")] string PinId,
    [property: JsonPropertyName("reason")] string? Reason);
