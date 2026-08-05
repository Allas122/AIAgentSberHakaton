using System.Text.Json;

namespace ChatNode.Infrastructure.AI.Functions;

public class RepeatCallGuard
{
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public bool IsRepeat(string tool, params string?[] arguments) =>
        !_seen.Add($"{tool}|{string.Join('|', arguments)}");

    public static string RepeatResponse(string tool) =>
        ToolJson.Serialize(new
        {
            Status = "Repeat",
            Message = $"Ты уже вызывал {tool} с такими же аргументами и получил ответ. " +
                      "Повторять запрещено: используй уже полученный результат, перейди к следующему " +
                      "шагу или заверши работу с этим фрагментом."
        });
}
