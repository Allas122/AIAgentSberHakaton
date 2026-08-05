using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ChatNode.Infrastructure.AI.Functions;

public static class ToolJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
