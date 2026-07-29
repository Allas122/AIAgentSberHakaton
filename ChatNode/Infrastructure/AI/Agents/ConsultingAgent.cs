using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.AI.Agents.Extensions;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.Dto;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;


namespace ChatNode.Infrastructure.AI.Agents;

public class ConsultingAgent: IAgent
{
    private static string[] _functionsName = ["get_manual_full_navigation", "search_in_manual", "send_status_of_processing"];
    private Guid _manualId;
    private IGigaChatClient _gigaChatClient;
    private IReadOnlyList<IChatFunctionTool> _functionTools;
    private Infrastructure.Configuration.Options.GigaChatOptions _gigaChatOptions;
    
    public ConsultingAgent(
        IManualRepository manualRepository,
        IGigaChatClient gigaChatClient,
        IOptions<Infrastructure.Configuration.Options.GigaChatOptions>  gigaChatOptions,
        Action<string> statusHandler,
        Guid manualId)
    {
        var manualTools = (new ManualFunctionsToolsSet(manualRepository, manualId)).FunctionTools;
        var aiAgentStatusTools = (new AiAgentStatusFunctionToolsSet(statusHandler)).FunctionTools;
        _functionTools = manualTools.Concat(aiAgentStatusTools).Where(s=>_functionsName.Contains(s.Name)).ToList();
        _gigaChatClient = gigaChatClient;
        _gigaChatOptions = gigaChatOptions.Value;
    }
    
    
    public async Task<string> InvokeAsync(string prompt, IEnumerable<MessageHistoricalDto> messages,CancellationToken ct)
    {
        List<Messages> chatMessages = [
            Messages.System(@"
Ты - Агент консультант, у тебя есть доступ к чтению из методички.
Твоя главная цель отвечать на вопросы клиентов, ты обязан ссылаться только на методичку и ничего более.
Что ты можешь делать:
1. get_manual_full_navigation - Для просмотра оглавления методички.
2. search_in_manual - Найти конкретный текст.
3. ОБЯЗАТЕЛЬНО: Перед каждым поиском или анализом вызывай send_status_of_processing, чтобы юзер не скучал.
")
        ];

        foreach (var message in messages)
        {
            var m = message.ToMessages();
            if(m==null) continue;
            chatMessages.Add(message.ToMessages());
        }
        
        chatMessages.Add(Messages.User(prompt));
        Chat chat = new Chat()
        {
            Messages = chatMessages,
            FunctionCall = FunctionCallMode.Auto,
            Model = _gigaChatOptions.ConsultingAgentModel
        };
        var res = await _gigaChatClient.ChatWithToolsAsync(chat, _functionTools, maxToolCalls: 40,cancellationToken:ct);
        return res.Message.Content;
    }
}