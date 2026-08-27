using System.Text;
using ChatNode.Infrastructure.AI.Functions;
using ChatNode.Infrastructure.AI.Functions.ReturnModels;
using ChatNode.Infrastructure.AI.Metering;
using ChatNode.Infrastructure.AI.Policy;
using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Analytics;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Dto;
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
    IDatasetRepository datasetRepository,
    IDatasetQueryRunner datasetQueryRunner,
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
        LetterContextDto context,
        LetterOutputMode mode,
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

        var datasetTools = new DatasetFunctionToolsSet(
            datasetRepository,
            datasetQueryRunner,
            null,
            loggerFactory.CreateLogger<DatasetFunctionToolsSet>());

        var tools = assignmentTools.FunctionTools
            .Where(tool => tool.Name == "create_assignment")
            .Concat(datasetTools.FunctionTools)
            .ToList();

        var chat = new Chat
        {
            Messages =
            [
                Messages.System(SystemPrompt(intent, template, context, mode)),
                Messages.User(Truncate(letter, MaxLetterChars))
            ],
            FunctionCall = FunctionCallMode.Auto,
            Model = _options.LetterAgentModel
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
            "ответ={ReplyChars} симв., заведено поручений={Assignments}, " +
            "карточка организации={HasOrganization}, обращение={Salutation}",
            session.SessionId,
            letter.Length,
            content?.Length ?? 0,
            assignmentTools.Created.Count,
            context.Organization is not null,
            context.Addressee?.Salutation ?? "не задано");

        return new LetterResult(
            string.IsNullOrWhiteSpace(content)
                ? "Сервис ИИ вернул пустой ответ. Попробуйте ещё раз."
                : content,
            assignmentTools.Created);
    }

    private static string SystemPrompt(
        string? intent,
        string? template,
        LetterContextDto context,
        LetterOutputMode mode)
    {
        if (mode == LetterOutputMode.BodyOnly) return BodyPrompt(template, intent, context);

        return string.IsNullOrWhiteSpace(template)
            ? DefaultPrompt(intent, context)
            : TemplatePrompt(template, intent, context);
    }

    private const string BodyRule = """
                                    Что писать:
                                    - Только текст письма по существу: от первого абзаца до последнего.
                                      Он ляжет на готовый бланк организации.
                                    - Абзацы разделяй переводом строки. Разметку markdown не используй:
                                      ни заголовков, ни списков, ни звёздочек — это машинописный текст письма.

                                    Чего НЕ писать (всё это уже напечатано на бланке, повтор будет ошибкой
                                    в официальном документе):
                                    - блок адресата и обращение «Уважаемый/Уважаемая …!»;
                                    - подпись, должность подписанта, блок «Контактное лицо», строку исполнителя;
                                    - строку «Приложение: …», исходящий номер и дату;
                                    - раздел «Требуется уточнить».

                                    Если для фразы не хватает данных, оставь на их месте метку в угловых скобках,
                                    например <указать срок>. Система покажет такие метки пользователю отдельно.
                                    """;

    private static string BodyPrompt(string? template, string? intent, LetterContextDto context) => $"""
Ты — помощник аппарата проректора. В сообщении пользователя приведён текст входящего письма.
Напиши текст ответа, который будет напечатан на бланке организации.
{OrganizationBlock(context.Organization)}{AddresseeBlock(context.Addressee)}
{BodyRule}

{ContactRule}

{AnonymizationRule}

{DatasetRule}

{AssignmentRule}
- В текст ответа поручения НЕ добавляй.
{BodyTemplateBlock(template)}{TemplateIntentBlock(intent)}
Верни только текст письма, без единого слова от себя.
""";

    private static string BodyTemplateBlock(string? template) =>
        string.IsNullOrWhiteSpace(template)
            ? string.Empty
            : $"""

               БЛАНК, НА КОТОРЫЙ ЛЯЖЕТ ОТВЕТ (для понимания окружения, писать его НЕ нужно):
               {Truncate(template.Trim(), MaxTemplateChars)}

               Напиши только то, что встанет на место метки <ТЕЛО>. Остальное подставит бланк.

               """;

    private static string OrganizationBlock(LetterOrganizationDto? organization)
    {
        if (organization is null)
        {
            return """

                   НАША ОРГАНИЗАЦИЯ: карточка не заполнена.
                   Подпись, контактное лицо и наименование организации ставь плейсхолдерами
                   <ПОДПИСЬ>, <КОНТАКТНОЕ ЛИЦО>, <НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ> и перечисли их
                   в конце под заголовком «Требуется уточнить».
                   Брать эти данные из входящего письма ЗАПРЕЩЕНО: там они принадлежат отправителю.

                   """;
        }

        var block = new StringBuilder();

        block.AppendLine();
        block.AppendLine("НАША ОРГАНИЗАЦИЯ — наши собственные данные, они верные:");
        AppendField(block, "Полное наименование", organization.FullName);
        AppendField(block, "Краткое наименование", organization.ShortName);
        AppendField(block, "Адрес", organization.Address);
        AppendField(block, "Телефон", organization.Phone);
        AppendField(block, "Факс", organization.Fax);
        AppendField(block, "Эл. почта организации", organization.Email);
        AppendField(block, "Сайт", organization.Website);
        AppendField(block, "ОКПО", organization.Okpo);
        AppendField(block, "ОГРН", organization.Ogrn);
        AppendField(block, "ИНН", organization.Inn);
        AppendField(block, "КПП", organization.Kpp);
        AppendField(block, "Должность подписанта", organization.SignerPosition);
        AppendField(block, "Подпись (ФИО)", organization.SignerName);
        AppendField(block, "Контактное лицо (ФИО)", organization.ContactName);
        AppendField(block, "Должность контактного лица", organization.ContactPosition);
        AppendField(block, "Телефон контактного лица", organization.ContactPhone);
        AppendField(block, "Эл. почта контактного лица", organization.ContactEmail);
        block.AppendLine();

        return block.ToString();
    }

    private static void AppendField(StringBuilder block, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) block.AppendLine($"- {label}: {value.Trim()}");
    }

    private const string ContactRule = """
                                       Контакты и подпись — железное правило:
                                       - Контактное лицо, его должность, телефон и почту, подпись и реквизиты бери ТОЛЬКО
                                         из блока «НАША ОРГАНИЗАЦИЯ».
                                       - Почта, телефоны, ФИО и реквизиты, встречающиеся во ВХОДЯЩЕМ письме, принадлежат
                                         ОТПРАВИТЕЛЮ. Поставить их в ответ как наши контакты — грубая ошибка.
                                       - ИНН, ОГРН, ОКПО и КПП — это реквизиты, а не контакты. В блок «Контактное лицо»
                                         они не идут никогда.
                                       - Если нужного поля в блоке «НАША ОРГАНИЗАЦИЯ» нет, оставь плейсхолдер и добавь его
                                         в «Требуется уточнить». Заполнять его данными из входящего письма ЗАПРЕЩЕНО.
                                       """;

    private static string AddresseeBlock(LetterAddresseeContextDto? addressee)
    {
        var unknown = addressee is null
                      || (string.IsNullOrWhiteSpace(addressee.Name)
                          && string.IsNullOrWhiteSpace(addressee.Position)
                          && string.IsNullOrWhiteSpace(addressee.Salutation));

        if (unknown)
        {
            return """

                   КОМУ ПИШЕМ: адресат не указан.
                   Поставь <АДРЕСАТ> и <ОБРАЩЕНИЕ> плейсхолдерами и добавь их в «Требуется уточнить».
                   Род обращения не угадывай.

                   """;
        }

        var block = new StringBuilder();

        block.AppendLine();
        block.AppendLine("КОМУ ПИШЕМ:");
        AppendField(block, "Должность адресата", addressee!.Position);
        AppendField(block, "ФИО адресата", addressee.Name);
        block.AppendLine();

        block.AppendLine(string.IsNullOrWhiteSpace(addressee.Salutation)
            ? "Обращение не определено: пол адресата по фамилии однозначно не выводится. "
              + "Поставь на месте обращения <ОБРАЩЕНИЕ> и добавь его в «Требуется уточнить». "
              + "НЕ пиши «Уважаемый(ая)» и НЕ выбирай род сам."
            : $"Обращение — используй ДОСЛОВНО, род и формулировку менять нельзя: {addressee.Salutation.Trim()}!");

        block.AppendLine();

        return block.ToString();
    }

    private const string PlaceholderRule = """
                                           Плейсхолдеры шаблона заполняются так:
                                           - <АДРЕСАТ> — должность и ФИО адресата из блока «КОМУ ПИШЕМ», в дательном падеже
                                             («Заместителю Министра ... Е.М. Грудининой»).
                                           - <ОБРАЩЕНИЕ> — строка обращения из блока «КОМУ ПИШЕМ», дословно, с восклицательным знаком.
                                           - <ПОДПИСЬ> — должность подписанта и ФИО из блока «НАША ОРГАНИЗАЦИЯ», одной строкой.
                                           - <КОНТАКТНОЕ ЛИЦО> — фраза вида «Контактное лицо — ФИО, должность, тел. ..., эл. почта: ...»
                                             из блока «НАША ОРГАНИЗАЦИЯ».
                                           - <ПОЛНОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ> и <КРАТКОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ> — оттуда же.
                                           - Остальные <...> заполняй по входящему письму и указанию пользователя.
                                             Если данных нет, оставь плейсхолдер как есть и внеси его в «Требуется уточнить».
                                           """;

    private const string DatasetRule = """
                                       Цифры из базы знаний:
                                       - Если в письме нужно назвать показатель — численность, сумму, долю, количество, —
                                         подними его через list_datasets и query_dataset, а не из головы.
                                       - Считает код, а не ты: полученное число переноси в текст дословно, не округляй
                                         и не пересчитывай.
                                       - Если подходящей таблицы нет, поставь плейсхолдер и внеси его
                                         в «Требуется уточнить». Выдумывать цифры в официальном письме ЗАПРЕЩЕНО.
                                       """;

    private const string AnonymizationRule = """
                                             Теги вида [SURNAME_0001], [EMAIL_00A3] во входящем письме — это скрытые персональные
                                             данные ОТПРАВИТЕЛЯ. Переноси их побуквенно, если они нужны по смыслу, и никогда
                                             не подставляй их в наши контакты и в подпись.
                                             """;

    private const string AssignmentRule = """
                                          Поручения:
                                          Если во входящем письме есть поручения, сроки или запросы, требующие от нас действия, —
                                          заведи их через create_assignment, по одному вызову на каждое, ДО того как писать ответ.
                                          - Формулировки бери из письма, ничего не выдумывай.
                                          - Если у поручения в письме назван СРОК — обязательно передай его в поле due_date.
                                            Дату пиши так, как она стоит в письме («15 сентября 2026 года» тоже понимается).
                                          - Если в письме назван ОТВЕТСТВЕННЫЙ — обязательно передай его в поле assignee.
                                          - В title не вставляй ни дату, ни фамилию: для них есть отдельные поля.
                                          - Не заводи поручение на то, что письмо просто сообщает к сведению.
                                          """;

    private static string TemplatePrompt(string template, string? intent, LetterContextDto context) => $"""
Ты — помощник аппарата проректора. В сообщении пользователя приведён текст входящего письма.
Составь ответ строго по шаблону, приведённому ниже.

ШАБЛОН ОТВЕТА:
{Truncate(template.Trim(), MaxTemplateChars)}
{OrganizationBlock(context.Organization)}{AddresseeBlock(context.Addressee)}
Правила:
- Ответ состоит ТОЛЬКО из блоков шаблона, в том же порядке и с теми же формулировками.
- Ничего не добавляй от себя: ни заголовков, ни разделов, ни списков, ни подписей,
  ни пояснений, которых нет в шаблоне. Даже если кажется, что так будет лучше.
- Ничего не выбрасывай: все блоки шаблона должны остаться на месте.
- Ничего не выдумывай: сроки, суммы, номера документов и фамилии бери только из письма.

{PlaceholderRule}

{ContactRule}

{AnonymizationRule}

{DatasetRule}

{AssignmentRule}
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

    private static string DefaultPrompt(string? intent, LetterContextDto context) => $"""
Ты — помощник аппарата проректора. В сообщении пользователя приведён текст входящего письма.
Составь на него официальный ответ на русском языке.
{OrganizationBlock(context.Organization)}{AddresseeBlock(context.Addressee)}
Требования к ответу:
- Деловой стиль официальной переписки: без разговорных оборотов, восклицаний и оценочных слов.
- Начни с блока адресата, следом — обращение из блока «КОМУ ПИШЕМ».
- Первым абзацем сошлись на поступившее обращение и кратко перечисли его суть.
- Далее по пунктам изложи позицию по каждому поставленному вопросу.
- Заверши блоком «Контактное лицо» и подписью из блока «НАША ОРГАНИЗАЦИЯ».
- Ничего не выдумывай: сроки, суммы, номера документов и фамилии бери только из письма
  или из указаний пользователя. Если данных не хватает — поставь плейсхолдер в угловых
  скобках, например <указать срок>, и перечисли такие места в конце под заголовком
  «Требуется уточнить».

{ContactRule}

{AnonymizationRule}

{DatasetRule}

{AssignmentRule}
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
