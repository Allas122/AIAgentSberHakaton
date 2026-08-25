using System.Text.Json;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using GigaChat.Net;
using GigaChat.Net.Models;

namespace ChatNode.Infrastructure.AI.Functions;

public class AiAgentStatusFunctionToolsSet(Func<string, Task> StatusHandler) : IFunctionToolsSet
{
    private const int MaxStatusLength = 80;
 
    public IReadOnlyList<IChatFunctionTool> FunctionTools => [
        FunctionTool.Create<StatusArguments>(
            name: "send_status_of_processing",
            description: @"Отправляет короткую техническую пометку о том, что ты СЕЙЧАС делаешь
                            (например: идёт поиск, идёт анализ). Это НЕ финальный ответ пользователю
                            и НЕ место для содержательного текста, приветствий, благодарностей или
                            прощаний — для этого просто ответь обычным текстом без вызова функций.
                            Вызывай эту функцию ТОЛЬКО непосредственно перед вызовом search_in_manual
                            или get_manual_full_navigation. Для простых реплик (привет/спасибо/прощание),
                            не требующих похода в методичку, эту функцию вызывать не нужно.",
            handler: SendStatus,
            parameters: FunctionParameter.Parameters(
                new Dictionary<string, FunctionParametersProperty>()
                {
                    ["status"] = FunctionParameter.String(
                        @"Короткая фраза в настоящем времени о процессе, до 80 символов.
                          Примеры: ""Ищу структуру заявки в методичке..."", ""Проверяю раздел про бюджет..."".
                          НЕ ответ пользователю, НЕ приветствие, НЕ прощание, НЕ выводы.")
                },
                required: ["status"]
            )
        )
    ];
 
    public async Task<string> SendStatus(StatusArguments arg)
    {
        string status = arg.Status ?? string.Empty;

        if (status.Length > MaxStatusLength)
        {
            status = status[..MaxStatusLength] + "...";
        }

        if (StatusHandler is not null) await StatusHandler(status);

        return ToolJson.Serialize(new StatusSendReturn("Статус отправлен!"));
    }
}