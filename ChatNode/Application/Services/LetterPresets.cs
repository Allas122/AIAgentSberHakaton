using ChatNode.Application.DTO;

namespace ChatNode.Application.Services;

public static class LetterPresets
{
    public static readonly Guid ResponsibleId = new("b3f1c0de-0001-4a10-9f00-6c1b2a7d0001");
    public static readonly Guid MonitoringId = new("b3f1c0de-0002-4a10-9f00-6c1b2a7d0002");
    public static readonly Guid ComplaintId = new("b3f1c0de-0003-4a10-9f00-6c1b2a7d0003");

    private const string Responsible = """
<АДРЕСАТ>

<ОБРАЩЕНИЕ>

В ответ на Ваше обращение <краткая суть обращения из входящего письма> сообщаем следующее.

Ответственным за <предмет обращения> в <КРАТКОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ> назначен <КОНТАКТНОЕ ЛИЦО>.

По всем вопросам, связанным с <предмет обращения>, просим обращаться к указанному лицу.

<ПОДПИСЬ>
""";

    private const string Monitoring = """
<АДРЕСАТ>

<ОБРАЩЕНИЕ>

В ответ на Ваш запрос <предмет запроса из входящего письма> <ПОЛНОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ> направляет запрашиваемую информацию. С предоставленной информацией можно ознакомиться в приложении к письму.

<КОНТАКТНОЕ ЛИЦО>

Приложение: в эл. виде.

<ПОДПИСЬ>
""";

    private const string Complaint = """
<АДРЕСАТ>

<ОБРАЩЕНИЕ>

Ваше обращение <дата и номер обращения, если они есть в письме> по вопросу <суть обращения> рассмотрено.

По изложенным в обращении обстоятельствам сообщаем следующее: <позиция по каждому доводу обращения>.

<принятые меры — заполнить, если они названы в указании пользователя>

Разъясняем, что принятое решение может быть обжаловано в порядке, установленном законодательством Российской Федерации.

<КОНТАКТНОЕ ЛИЦО>

<ПОДПИСЬ>
""";

    private static readonly IReadOnlyList<LetterTemplateDto> Items =
    [
        Preset(ResponsibleId, "Ответ: ответственный и контакты", Responsible),
        Preset(MonitoringId, "Сопроводительное к мониторингу", Monitoring),
        Preset(ComplaintId, "Ответ на жалобу", Complaint)
    ];

    public static IReadOnlyList<LetterTemplateDto> All => Items;

    public static bool IsPreset(Guid templateId) => Items.Any(item => item.Id == templateId);

    public static LetterTemplateDto? Find(Guid templateId) => Items.FirstOrDefault(item => item.Id == templateId);

    private static LetterTemplateDto Preset(Guid id, string name, string content) =>
        new(id,
            name,
            content.Replace("\r\n", "\n").Trim(),
            SourceFileName: null,
            CreatedAt: default,
            UpdatedAt: default,
            IsPreset: true);
}
