using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.AI.Agents.Extensions;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.Dto;
using Domain.Repositories;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public class ConsultingAgent : IAgent
{
    private static string[] _functionsName =
    [
        "get_manual_full_navigation",
        "search_in_manual",
        "send_status_of_processing",
        "get_pins",
        "search_pins"
    ];

    private IGigaChatClient _gigaChatClient;
    private IManualRepository _manualRepository;
    private IPinRepository _pinRepository;
    private Action<string> _statusHandler;
    private AgentSession _session;
    private Infrastructure.Configuration.Options.GigaChatOptions _gigaChatOptions;

    public ConsultingAgent(
        IManualRepository manualRepository,
        IPinRepository pinRepository,
        IGigaChatClient gigaChatClient,
        IOptions<Infrastructure.Configuration.Options.GigaChatOptions> gigaChatOptions,
        Action<string> statusHandler,
        AgentSession session)
    {
        _manualRepository = manualRepository;
        _pinRepository = pinRepository;
        _statusHandler = statusHandler;
        _session = session;
        _gigaChatClient = gigaChatClient;
        _gigaChatOptions = gigaChatOptions.Value;
    }
    
    private IReadOnlyList<IChatFunctionTool> BuildTools()
    {
        var manualTools = (new ManualFunctionsToolsSet(_manualRepository, _session.ManualId)).FunctionTools;
        var pinTools = (new PinFunctionToolsSet(
            _pinRepository,
            _session.ChatId,
            _gigaChatOptions.PinDuplicateDistance,
            _gigaChatOptions.PinSearchDistance)).FunctionTools;
        var aiAgentStatusTools = (new AiAgentStatusFunctionToolsSet(_statusHandler)).FunctionTools;

        return manualTools
            .Concat(pinTools)
            .Concat(aiAgentStatusTools)
            .Where(s => _functionsName.Contains(s.Name))
            .ToList();
    }

    public async Task<string> InvokeAsync(string prompt, IEnumerable<MessageHistoricalDto> messages, CancellationToken ct)
    {
        List<Messages> chatMessages = [
            Messages.System(@"
Ты - Агент консультант, у тебя есть доступ к чтению из методички.
Твоя главная цель отвечать на вопросы клиентов, ты обязан ссылаться только на методичку и ничего более.

Что ты можешь делать:
1. get_manual_full_navigation - Для просмотра оглавления методички.
2. search_in_manual - Найти конкретный текст.
3. ОБЯЗАТЕЛЬНО: Перед каждым поиском или анализом вызывай send_status_of_processing, чтобы юзер не скучал.
4. get_pins и search_pins - Посмотреть замечания по заявке, которую проверяли в этом чате.

Про замечания по заявке:
Если в этом чате пользователь загружал заявку на проверку, результаты проверки лежат в заметках.
Категории заметок: Mistake - подтверждённое нарушение требований методички, Attention - спорный
момент, WhatToCheck - то, что осталось проверить, Summary - краткое содержание раздела заявки.
- Когда пользователь спрашивает ""что было не так"", ""почему это ошибка"", ""как исправить"" —
  подними замечания через get_pins или найди нужное через search_pins, и обсуждай именно их.
- Заметку мало пересказать: подними через search_in_manual то требование методички, из-за которого
  замечание возникло, и объясни пользователю, что именно исправить и почему.
- Менять и удалять заметки ты не можешь — это только результат проверки, доступный тебе на чтение.
  Если пользователь не согласен с замечанием, разбери его по методичке и честно скажи, что нашёл,
  но не обещай ""убрать"" или ""исправить"" замечание.
- Если заметок нет — значит, в этом чате заявку ещё не проверяли. Так и скажи и предложи её загрузить.
  Не выдумывай замечания и не разбирай заявку, которой не видел.

КРИТИЧЕСКИ ВАЖНО про историю переписки:
В истории диалога сохраняются только твои прошлые текстовые ответы пользователю — реальный текст,
который ты доставал из методички через search_in_manual в прошлых ходах, тебе НЕ виден и не сохранён.
Поэтому:
- Если пользователь просит уточнить, развернуть, ""написать подробнее"", ""прислать текст"" то, что
  обсуждалось раньше — это ЗНАЧИТ, что нужно вызвать search_in_manual заново с тем же или более точным
  запросом, а не пересказывать своими словами свой же прошлый ответ из истории. Твой прошлый ответ в
  истории мог быть неполным — не считай его источником истины, источник истины только методичка.
- Никогда не перефразируй и не ""сжимай"" свой предыдущий ответ вместо повторного похода в методичку,
  если пользователь просит детали или полный текст.

Запрет на выдумывание:
- Отвечай только тем, что реально нашёл через search_in_manual в ЭТОМ ходе диалога.
- Если после поиска (в том числе с разными формулировками запроса) релевантного текста не нашлось —
  прямо скажи пользователю, что не нашёл эту информацию в методичке, и не заполняй пустоту общими
  фразами о грантах и заявках. Общие фразы без опоры на найденный текст запрещены.
- Если структура/правило состоит из нескольких частей (например, было разделено на ""Часть 1"", ""Часть 2""
  при индексации) — ищи и объединяй все части перед ответом, а не ограничивайся первым найденным фрагментом.
")
        ];

        foreach (var message in messages)
        {
            var m = message.ToMessages();
            if (m == null) continue;
            chatMessages.Add(m);
        }

        chatMessages.Add(Messages.User(prompt));
        Chat chat = new Chat()
        {
            Messages = chatMessages,
            FunctionCall = FunctionCallMode.Auto,
            Model = _gigaChatOptions.ConsultingAgentModel
        };

        var res = await GigaChatRetry.ExecuteAsync(
            token => _gigaChatClient.ChatWithToolsAsync(chat, BuildTools(), maxToolCalls: 40, cancellationToken: token),
            "ответ консультанта",
            _gigaChatOptions.MaxOperationAttempts,
            _gigaChatOptions.OperationRetryDelaySeconds,
            ct);

        if (string.IsNullOrWhiteSpace(res.Message.Content))
        {
            return "Не удалось получить ответ из методички — попробуйте переформулировать вопрос или повторить запрос.";
        }

        return res.Message.Content;
    }

    public Task<string> InvokeAsync(string prompt, CancellationToken ct)
    {
        return InvokeAsync(prompt, new List<MessageHistoricalDto>(), ct);
    }
}