using System.Text.Json.Serialization;

namespace ChatNode.Infrastructure.AI.Functions.Arguments;

public record ReviewLookupArguments(
    [property: JsonPropertyName("status")] string? Status);
