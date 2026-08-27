using ChatNode.Infrastructure.AI.Agents.Abstractions;
using ChatNode.Infrastructure.AI.Agents.Extensions;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Analytics;
using ChatNode.Infrastructure.Dto;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public class ConsultingAgent : IAgent
{
    private static readonly string[] SharedFunctions =
    [
        "get_manual_full_navigation",
        "search_in_manual",
        "send_status_of_processing",
        "get_pins",
        "search_pins",
        "list_assignments",
        "get_application_review",
        "get_current_datetime",
        "resolve_date",
        "date_difference",
        "list_datasets",
        "query_dataset"
    ];

    private static readonly string[] StaffFunctions =
    [
        "create_assignment",
        "update_assignment_status"
    ];

    private IGigaChatClient _gigaChatClient;
    private IManualRepository _manualRepository;
    private IPinRepository _pinRepository;
    private IAssignmentRepository _assignmentRepository;
    private IUserRepository _userRepository;
    private IReviewRepository _reviewRepository;
    private IDatasetRepository _datasetRepository;
    private IDatasetQueryRunner _datasetQueryRunner;
    private IAnonymizeClient _anonymizeClient;
    private ITokenMeter _tokenMeter;
    private Func<string, Task> _statusHandler;
    private AgentSession _session;
    private Infrastructure.Configuration.Options.GigaChatOptions _gigaChatOptions;
    private ILoggerFactory _loggerFactory;
    private ILogger<ConsultingAgent> _logger;

    public ConsultingAgent(
        IManualRepository manualRepository,
        IPinRepository pinRepository,
        IAssignmentRepository assignmentRepository,
        IUserRepository userRepository,
        IReviewRepository reviewRepository,
        IDatasetRepository datasetRepository,
        IDatasetQueryRunner datasetQueryRunner,
        IAnonymizeClient anonymizeClient,
        ITokenMeter tokenMeter,
        IGigaChatClient gigaChatClient,
        IOptions<Infrastructure.Configuration.Options.GigaChatOptions> gigaChatOptions,
        ILoggerFactory loggerFactory,
        Func<string, Task> statusHandler,
        AgentSession session)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ConsultingAgent>();
        _manualRepository = manualRepository;
        _pinRepository = pinRepository;
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _reviewRepository = reviewRepository;
        _datasetRepository = datasetRepository;
        _datasetQueryRunner = datasetQueryRunner;
        _anonymizeClient = anonymizeClient;
        _tokenMeter = tokenMeter;
        _statusHandler = statusHandler;
        _session = session;
        _gigaChatClient = gigaChatClient;
        _gigaChatOptions = gigaChatOptions.Value;
    }

    private IReadOnlyList<IChatFunctionTool> BuildTools()
    {
        var manualTools = _session.ManualId is { } manualId
            ? (new ManualFunctionsToolsSet(
                _manualRepository,
                manualId,
                _loggerFactory.CreateLogger<ManualFunctionsToolsSet>())).FunctionTools
            : [];

        var pinTools = (new PinFunctionToolsSet(
            _pinRepository,
            _session.ChatId,
            _loggerFactory.CreateLogger<PinFunctionToolsSet>(),
            _gigaChatOptions.PinDuplicateDistance,
            _gigaChatOptions.PinSearchDistance)).FunctionTools;
        var aiAgentStatusTools = (new AiAgentStatusFunctionToolsSet(_statusHandler)).FunctionTools;
        var assignmentTools = (new AssignmentFunctionToolsSet(
            _assignmentRepository,
            _userRepository,
            _anonymizeClient,
            _session.UserId,
            _session.ChatId.ToString(),
            AssignmentSource.Manual,
            _loggerFactory.CreateLogger<AssignmentFunctionToolsSet>())).FunctionTools;

        var reviewTools = (new ReviewFunctionToolsSet(
            _reviewRepository,
            _anonymizeClient,
            _session.ChatId,
            _session.UserId,
            _session.ChatId.ToString())).FunctionTools;

        var calendarTools = (new CalendarFunctionToolsSet(
            TimeProvider.System,
            _session.ResolveTimeZone(_gigaChatOptions.AgentTimeZoneId))).FunctionTools;

        var datasetTools = (new DatasetFunctionToolsSet(
            _datasetRepository,
            _datasetQueryRunner,
            IsStaffChat ? null : ManualScope.Public,
            _loggerFactory.CreateLogger<DatasetFunctionToolsSet>())).FunctionTools;

        var allowed = IsStaffChat ? [.. SharedFunctions, .. StaffFunctions] : SharedFunctions;

        return manualTools
            .Concat(pinTools)
            .Concat(aiAgentStatusTools)
            .Concat(assignmentTools)
            .Concat(reviewTools)
            .Concat(calendarTools)
            .Concat(datasetTools)
            .Where(s => allowed.Contains(s.Name))
            .ToList();
    }

    private bool IsStaffChat => _session.Kind == ChatKind.Staff;

    private bool HasManual => _session.ManualId is not null;

    private List<Messages> BuildMessages(string prompt, IEnumerable<MessageHistoricalDto> messages)
    {
        List<Messages> chatMessages = [Messages.System(BuildSystemPrompt())];

        foreach (var message in messages)
        {
            var m = message.ToMessages();
            if (m == null) continue;
            chatMessages.Add(m);
        }

        chatMessages.Add(Messages.User(prompt));

        return chatMessages;
    }

    public async Task<string> InvokeAsync(string prompt, IEnumerable<MessageHistoricalDto> messages, CancellationToken ct)
    {
        Chat chat = new Chat()
        {
            Messages = BuildMessages(prompt, messages),
            FunctionCall = FunctionCallMode.Auto,
            Model = _gigaChatOptions.ConsultingAgentModel
        };

        var res = await _tokenMeter.MeasureToolsAsync(
            TokenOperation.Consulting,
            chat.Model,
            () => GigaChatRetry.ExecuteAsync(
                token => _gigaChatClient.ChatWithToolsAsync(chat, BuildTools(), maxToolCalls: 40, cancellationToken: token),
                "ответ консультанта",
                _gigaChatOptions.MaxOperationAttempts,
                _gigaChatOptions.OperationRetryDelaySeconds,
                _logger,
                ct),
            _session.ChatId,
            _session.UserId);

        if (string.IsNullOrWhiteSpace(res.Message.Content))
        {
            return "Не удалось получить ответ из методички — попробуйте переформулировать вопрос или повторить запрос.";
        }

        return res.Message.Content;
    }

    private string BuildSystemPrompt() => Head + Datasets + Assignments + Tail;

    private string Datasets => @"
Про таблицы базы знаний (мониторинги, выгрузки, реестры):
- list_datasets показывает загруженные таблицы, их колонки и примеры значений. Вызывай его
  ПЕРВЫМ, прежде чем считать что-либо: без точных названий колонок запрос не построить.
- query_dataset считает по таблице: сколько строк подходит под условия, сумму, среднее,
  минимум, максимум, при необходимости с разбивкой по группам.
- Числа СЧИТАЕТ КОД, а не ты. Полученное значение переноси в ответ дословно и не пересчитывай,
  не округляй и не прикидывай на глаз. Сам по строкам таблицы не считай никогда.
- Если в ответе инструмента есть notes, перескажи их пользователю: там сказано, какие строки
  пропущены и каких колонок не нашлось.
- Если подходящей таблицы нет, честно скажи об этом и предложи загрузить мониторинг
  в формате .csv или .xlsx — выдумывать цифры запрещено.
";

    private string Head => HasManual
        ? @"
Ты - Агент консультант, у тебя есть доступ к чтению из документа базы знаний.
Твоя главная цель отвечать на вопросы клиентов. Отвечая про требования и правила, опирайся
только на этот документ и ничего более: своих знаний о них у тебя нет.

Что ты можешь делать:
1. get_manual_full_navigation - Для просмотра оглавления документа.
2. search_in_manual - Найти конкретный текст.
3. ОБЯЗАТЕЛЬНО: Перед каждым поиском или анализом вызывай send_status_of_processing, чтобы юзер не скучал.
4. get_pins и search_pins - Посмотреть замечания по заявке, которую проверяли в этом чате.
5. list_assignments - Посмотреть поручения пользователя.
6. get_application_review - Поднять результат проверки заявки: баллы по критериям и замечания.
7. list_datasets и query_dataset - Посчитать по таблицам базы знаний: мониторингам, выгрузкам, реестрам.
   Это отдельный источник, он не противоречит пункту про документ: цифры берутся оттуда.
"
        : @"
Ты - Агент консультант в служебном чате сотрудников.
Методичка к этому чату не подключена, искать по ней ты не можешь.

Что ты можешь делать:
1. ОБЯЗАТЕЛЬНО: Перед каждым поиском или анализом вызывай send_status_of_processing, чтобы юзер не скучал.
2. create_assignment - Завести поручение. Это твоя основная работа в этом чате.
3. update_assignment_status - Сменить состояние поручения.
4. list_assignments - Посмотреть поручения пользователя.
5. get_pins и search_pins - Посмотреть замечания по заявке, которую проверяли в этом чате.
6. get_application_review - Поднять результат проверки заявки: баллы по критериям и замечания.
7. list_datasets и query_dataset - Посчитать по таблицам базы знаний.

Не ссылайся на методичку и не обещай найти в ней текст. Если вопрос упирается в её требования,
честно скажи, что в служебном чате её нет, и предложи открыть чат с выбранной методичкой.
";

    private string Assignments => IsStaffChat
        ? @"
Про поручения:
Это служебный чат, поручения ты заводишь сам.
- На вопросы ""что у меня висит"", ""что в работе"", ""что просрочено"" отвечай по list_assignments,
  а не по памяти. Для ""просрочено"" и ""сколько осталось"" вызывай date_difference: она сама
  сравнит срок с сегодняшним днём и посчитает дни, рабочие дни и полные месяцы.
  Свою память о сегодняшнем числе не используй, она неверна — бери её из get_current_datetime.
- Расплывчатый срок (""конец недели"", ""через две недели"", ""к концу месяца"") переводи в дату
  через resolve_date и клади в due_date уже как ГГГГ-ММ-ДД. В уме не считай.
- create_assignment - завести поручение, по одному вызову на каждое. Формулировку бери из просьбы
  пользователя, а не придумывай свою.
- Исполнителя указывай в поле assignee ровно так, как назвал пользователь: фамилия с инициалами
  или должность. Если он назвал сотрудника, у которого есть учётная запись, поручение свяжется
  с ней автоматически - подбирать идентификаторы тебе не нужно.
- Если пользователь не сказал, кому поручение, оставь assignee пустым и спроси, на кого его записать.
  Не назначай исполнителя по своей догадке.
- Срок клади в due_date, а не в формулировку.
- update_assignment_status - сменить состояние поручения. Идентификатор бери из list_assignments,
  по памяти его не сочиняй.
- Заведя поручение, коротко подтверди пользователю: что записано, на кого и к какому сроку.
- Удалять поручения ты не можешь — это делается в разделе «Поручения».
- Если поручений нет, так и скажи. Не выдумывай их.
"
        : @"
Про поручения:
Поручения заводятся при разборе входящих писем и вручную. Тебе они доступны только на чтение.
- На вопросы ""что у меня висит"", ""что в работе"", ""что просрочено"" отвечай по list_assignments,
  а не по памяти. Для ""просрочено"" и ""сколько осталось"" вызывай date_difference: она сама
  сравнит срок с сегодняшним днём и посчитает дни, рабочие дни и полные месяцы.
  Свою память о сегодняшнем числе не используй, она неверна — бери её из get_current_datetime.
- Расплывчатый срок (""конец недели"", ""через две недели"", ""к концу месяца"") переводи в дату
  через resolve_date и клади в due_date уже как ГГГГ-ММ-ДД. В уме не считай.
- Заводить, менять и удалять поручения ты не можешь. Если пользователь просит — скажи, что это
  делается в разделе «Поручения» или при разборе письма.
- Если поручений нет, так и скажи. Не выдумывай их.
";

    private string ExplainByManual => HasManual
        ? @"- Объясняя замечание, обязательно подними через search_in_manual то требование методички,
  из-за которого оно возникло, и скажи, что конкретно переписать в заявке. Пересказать замечание
  недостаточно — пользователю нужно понимать, что делать."
        : @"- Объясняя замечание, опирайся на его текст и на ссылки на методичку внутри него: искать
  по самой методичке в этом чате нечем. Скажи, что конкретно переписать в заявке, и не додумывай
  требований, которых в замечании нет.";

    private string ExplainPinByManual => HasManual
        ? @"- Заметку мало пересказать: подними через search_in_manual то требование методички, из-за которого
  замечание возникло, и объясни пользователю, что именно исправить и почему."
        : @"- Заметку мало пересказать: объясни пользователю, что именно исправить и почему, по тексту самой
  заметки. Требования методички в этом чате поднять нечем — не выдумывай их.";

    private string HistoryBlock => HasManual
        ? @"КРИТИЧЕСКИ ВАЖНО про историю переписки:
В истории диалога сохраняются только твои прошлые текстовые ответы пользователю — реальный текст,
который ты доставал из методички через search_in_manual в прошлых ходах, тебе НЕ виден и не сохранён.
Поэтому:
- Если пользователь просит уточнить, развернуть, ""написать подробнее"", ""прислать текст"" то, что
  обсуждалось раньше — это ЗНАЧИТ, что нужно вызвать search_in_manual заново с тем же или более точным
  запросом, а не пересказывать своими словами свой же прошлый ответ из истории. Твой прошлый ответ в
  истории мог быть неполным — не считай его источником истины, источник истины только методичка.
- Никогда не перефразируй и не ""сжимай"" свой предыдущий ответ вместо повторного похода в методичку,
  если пользователь просит детали или полный текст.
"
        : @"КРИТИЧЕСКИ ВАЖНО про историю переписки:
В истории диалога сохраняются только твои прошлые текстовые ответы пользователю. Данные, которые ты
поднимал инструментами в прошлых ходах, тебе НЕ видны и не сохранены.
- Если пользователь просит уточнить или развернуть то, что обсуждалось раньше, подними данные
  инструментами заново, а не пересказывай свой же прошлый ответ из истории.
";

    private string InventionBlock => HasManual
        ? @"Запрет на выдумывание:
- Отвечай только тем, что реально нашёл через search_in_manual в ЭТОМ ходе диалога.
- Если после поиска (в том числе с разными формулировками запроса) релевантного текста не нашлось —
  прямо скажи пользователю, что не нашёл эту информацию в документе, и не заполняй пустоту общими
  фразами по теме. Общие фразы без опоры на найденный текст запрещены.
- Если структура/правило состоит из нескольких частей (например, было разделено на ""Часть 1"", ""Часть 2""
  при индексации) — ищи и объединяй все части перед ответом, а не ограничивайся первым найденным фрагментом.
"
        : @"Запрет на выдумывание:
- Отвечай только тем, что реально подняли инструменты в ЭТОМ ходе диалога.
- Если данных нет, прямо скажи об этом и не заполняй пустоту общими фразами по теме.
  Общие фразы без опоры на поднятые данные запрещены.
";

    private string Tail => $@"

Про оценку заявки (главное про разбор ошибок):
Результат проверки — это оценка по критериям методички. У каждого критерия есть вывод и балл.
- На вопросы ""сколько баллов"", ""что не так с заявкой"", ""почему такая оценка"", ""как исправить""
  сначала вызывай get_application_review, и только потом отвечай. По памяти не отвечай никогда.
- Чтобы разобрать именно ошибки, вызывай get_application_review со status=Violated,
  спорные места — status=Questionable.
{ExplainByManual}
- Вывод ""Раздел не найден"" означает, что автоматическая сверка оглавления не нашла подходящего
  раздела. Это повод проверить вручную, а не доказанное отсутствие. Так и говори, не обвиняй заявку.
- Вывод ""Замечаний нет"" означает, что нарушений не зафиксировано. Не выдавай это за подтверждённое
  качество заявки и не придумывай ей достоинств.
- Балл менять ты не можешь. Если пользователь не согласен — разбери замечание по методичке
  и честно скажи, что нашёл, но не обещай пересчитать оценку.
- Если проверок не было, так и скажи и предложи загрузить заявку. Не выдумывай замечания.

Про заметки проверки:
Если в этом чате пользователь загружал заявку на проверку, черновые находки лежат в заметках.
Категории заметок: Mistake - подтверждённое нарушение требований методички, Attention - спорный
момент, WhatToCheck - то, что осталось проверить, Summary - краткое содержание раздела заявки.
- Когда пользователь спрашивает ""что было не так"", ""почему это ошибка"", ""как исправить"" —
  подними замечания через get_pins или найди нужное через search_pins, и обсуждай именно их.
{ExplainPinByManual}
- Менять и удалять заметки ты не можешь — это только результат проверки, доступный тебе на чтение.
  Если пользователь не согласен с замечанием, разбери его по методичке и честно скажи, что нашёл,
  но не обещай ""убрать"" или ""исправить"" замечание.
- Если заметок нет — значит, в этом чате заявку ещё не проверяли. Так и скажи и предложи её загрузить.
  Не выдумывай замечания и не разбирай заявку, которой не видел.

{HistoryBlock}
КРИТИЧЕСКИ ВАЖНО про скрытые персональные данные:
Фамилии, имена, телефоны и прочие персональные данные заменены тегами вида [SURNAME_0001],
[GIVEN_NAME_0004], [EMAIL_00A3]. Это не опечатки и не заглушки — настоящие значения будут
подставлены обратно автоматически, уже после твоего ответа.
- Переноси такой тег в ответ ПОБУКВЕННО, ровно как он пришёл: та же длина, тот же тип, то же число.
- Категорически запрещено переписывать тег своими словами: [YOUR NAME_0004], [ФАМИЛИЯ_0004],
  [имя] — всё это ломает подстановку, и пользователь увидит мусор вместо фамилии.
- Не склоняй тег, не переводи, не сокращай, не бери в кавычки и не дополняй его.
- Если не уверен, что скопировал тег точно, лучше построй фразу так, чтобы он не понадобился.

{InventionBlock}";

    public Task<string> InvokeAsync(string prompt, CancellationToken ct)
    {
        return InvokeAsync(prompt, new List<MessageHistoricalDto>(), ct);
    }
}