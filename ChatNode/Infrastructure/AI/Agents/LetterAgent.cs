using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Configuration.Options;
using Domain.Repositories;
using Domain.ValueTypes;
using GigaChat.Net;
using GigaChat.Net.Models;
using Microsoft.Extensions.Options;
using Chat = GigaChat.Net.Models.Chat;

namespace ChatNode.Infrastructure.AI.Agents;

public record LetterResult(string Reply, IReadOnlyList<AssignmentView> Assignments);

public class LetterAgent(
    IGigaChatClient gigaChatClient,
    IOptions<GigaChatOptions> gigaChatOptions,
    IAssignmentRepository assignmentRepository,
    IUserRepository userRepository,
    IAnonymizeClient anonymizeClient,
    ITokenMeter tokenMeter,
    ILoggerFactory loggerFactory,
    LetterSession session)
{
    private const int MaxLetterChars = 12000;
    private const int MaxIntentChars = 1000;
    private const int MaxTemplateChars = 8000;
    private const int MaxToolCalls = 20;

    private readonly GigaChatOptions _options = gigaChatOptions.Value;

    private readonly ILogger<LetterAgent> _logger = loggerFactory.CreateLogger<LetterAgent>();

    public async Task<LetterResult> ComposeReplyAsync(
        string letter,
        string? intent,
        string? template,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(letter))
        {
            return new LetterResult(
                "Не удалось прочитать текст письма — файл пустой или состоит только из изображений.",
                []);
        }

        var assignmentTools = new AssignmentFunctionToolsSet(
            assignmentRepository,
            userRepository,
            anonymizeClient,
            session.OwnerId,
            session.SessionId,
            AssignmentSource.Letter,
            loggerFactory.CreateLogger<AssignmentFunctionToolsSet>());

        var tools = assignmentTools.FunctionTools
            .Where(tool => tool.Name == "create_assignment")
            .ToList();

        var chat = new Chat
        {
            Messages =
            [
                Messages.System(SystemPrompt(intent, template)),
                Messages.User(Truncate(letter, MaxLetterChars))
            ],
            FunctionCall = FunctionCallMode.Auto,
            Model = _options.ConsultingAgentModel
        };

        var result = await tokenMeter.MeasureToolsAsync(
            TokenOperation.Letter,
            chat.Model,
            () => GigaChatRetry.ExecuteAsync(
                token => gigaChatClient.ChatWithToolsAsync(chat, tools, maxToolCalls: MaxToolCalls, cancellationToken: token),
                "ответ на письмо",
                _options.MaxOperationAttempts,
                _options.OperationRetryDelaySeconds,
                _logger,
                ct),
            userId: session.OwnerId);

        var content = result.Message.Content?.Trim();

        _logger.LogInformation(
            "Ответ на письмо готов: сессия={SessionId}, письмо={LetterChars} симв., " +
            "ответ={ReplyChars} симв., заведено поручений={Assignments}",
            session.SessionId,
            letter.Length,
            content?.Length ?? 0,
            assignmentTools.Created.Count);

        return new LetterResult(
            string.IsNullOrWhiteSpace(content)
                ? "Сервис ИИ вернул пустой ответ. Попробуйте ещё раз."
                : content,
            assignmentTools.Created);
    }

    private static string SystemPrompt(string? intent, string? template) =>
        string.IsNullOrWhiteSpace(template)
            ? DefaultPrompt(intent)
            : TemplatePrompt(template, intent);

    private static string TemplatePrompt(string template, string? intent) => $"""
Ты — помощник аппарата проректора. В сообщении пользователя приведён текст входящего письма.
Составь ответ строго по шаблону, приведённому ниже.

ШАБЛОН ОТВЕТА:
{Truncate(template.Trim(), MaxTemplateChars)}

Правила:
- Ответ состоит ТОЛЬКО из блоков шаблона, в том же порядке и с теми же формулировками.
- Ничего не добавляй от себя: ни заголовков, ни разделов, ни списков, ни подписей,
  ни пояснений, которых нет в шаблоне. Даже если кажется, что так будет лучше.
- Ничего не выбрасывай: все блоки шаблона должны остаться на месте.
- Меняй только те места, которые зависят от письма, и подставляй в них данные из письма.
- Места вида [указать срок], ___ или <поле> — поля для заполнения. Если данных для поля
  в письме нет, оставь его ровно так, как оно стоит в шаблоне, и молча иди дальше.
- Ничего не выдумывай: сроки, суммы, номера документов и фамилии бери только из письма.
- Теги вида [SURNAME_0001], [EMAIL_00A3] — это скрытые персональные данные. Переноси их
  побуквенно, они будут восстановлены автоматически.

Поручения:
Если во входящем письме есть поручения, сроки или запросы, требующие от нас действия, —
заведи их через create_assignment, по одному вызову на каждое, ДО того как писать ответ.
- Формулировки бери из письма, ничего не выдумывай.
- Если у поручения в письме назван СРОК — обязательно передай его в поле due_date.
  Дату пиши так, как она стоит в письме («15 сентября 2026 года» тоже понимается).
- Если в письме назван ОТВЕТСТВЕННЫЙ — обязательно передай его в поле assignee.
- В title не вставляй ни дату, ни фамилию: для них есть отдельные поля.
- Не заводи поручение на то, что письмо просто сообщает к сведению.
- В текст ответа поручения НЕ добавляй: ответ должен остаться строго по шаблону.
{TemplateIntentBlock(intent)}
Верни только текст ответа по шаблону, без единого слова от себя.
""";

    private static string TemplateIntentBlock(string? intent) =>
        string.IsNullOrWhiteSpace(intent)
            ? string.Empty
            : $"""

               Указание пользователя — учти его при заполнении полей шаблона,
               но структуру шаблона не меняй:
               {Truncate(intent.Trim(), MaxIntentChars)}

               """;

    private static string DefaultPrompt(string? intent) => $"""
Ты — помощник аппарата проректора. В сообщении пользователя приведён текст входящего письма.
Составь на него официальный ответ на русском языке.

Требования к ответу:
- Деловой стиль официальной переписки: без разговорных оборотов, восклицаний и оценочных слов.
- Начни с обращения. Если имя адресата в письме не указано, используй «Уважаемый(ая) коллега».
- Первым абзацем сошлись на поступившее обращение и кратко перечисли его суть.
- Далее по пунктам изложи позицию по каждому поставленному вопросу.
- Заверши формулой вежливости и блоком подписи в виде плейсхолдеров:
  «Проректор ______________ И.О. Фамилия» и строкой для исполнителя.
- Ничего не выдумывай: сроки, суммы, номера документов и фамилии бери только из письма
  или из указаний пользователя. Если данных не хватает — поставь плейсхолдер в квадратных
  скобках, например [указать срок], и перечисли такие места в конце под заголовком
  «Требуется уточнить».
- Теги вида [SURNAME_0001], [EMAIL_00A3] — это скрытые персональные данные. Переноси их
  в ответ как есть, не заменяя и не додумывая: они будут восстановлены автоматически.

Поручения:
Если во входящем письме есть поручения, сроки или запросы, требующие от нас действия, —
заведи их через create_assignment, по одному вызову на каждое, ДО того как писать ответ.
- Формулировки бери из письма, ничего не выдумывай.
- Если у поручения в письме назван СРОК — обязательно передай его в поле due_date.
  Дату пиши так, как она стоит в письме («15 сентября 2026 года» тоже понимается).
  Не оставляй срок только внутри текста формулировки: поле due_date должно быть заполнено.
- Если в письме назван ОТВЕТСТВЕННЫЙ — обязательно передай его в поле assignee.
- В title не вставляй ни дату, ни фамилию: для них есть отдельные поля.
- Не заводи поручение на то, что письмо просто сообщает к сведению.
- Заведённые поручения перечисли в конце ответа под заголовком «Поручения на контроль».
- Если действий от нас письмо не требует, не вызывай create_assignment вообще.
{IntentBlock(intent)}
Верни только текст письма без пояснений от себя.
""";

    private static string IntentBlock(string? intent) =>
        string.IsNullOrWhiteSpace(intent)
            ? "- Тон ответа выбери нейтрально-деловой, по существу обращения."
            : $"""
               Указание пользователя, какой ответ нужен (соблюдай его в первую очередь):
               {Truncate(intent.Trim(), MaxIntentChars)}
               """;

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
