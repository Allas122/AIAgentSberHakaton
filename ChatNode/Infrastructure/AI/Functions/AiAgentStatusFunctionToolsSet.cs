using System.Text.Json;
using ChatNode.Infrastructure.AI.Functions.Abstractions;
using ChatNode.Infrastructure.AI.Functions.Arguments;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using GigaChat.Net;

namespace ChatNode.Infrastructure.AI.Functions;

public class AiAgentStatusFunctionToolsSet(Action<string> StatusHandler) : IFunctionToolsSet
{
    public IReadOnlyList<IChatFunctionTool> FunctionTools => [
        FunctionTool.Create<StatusArguments>(
            name: "send_status_of_processing",
            description: @"Вывести свои мысли или статус проверки для пользователя.",
            handler: SendStatus
            )
    ];
    public string SendStatus(StatusArguments arg)
    {
        StatusHandler?.Invoke(arg.Status);
        return JsonSerializer.Serialize(new StatusSendReturn("Статус отправлен !"));
    }
}