using ChatNode.Api.Rest.Messages.Organization;
using FluentValidation;

namespace ChatNode.Api.Rest.Validators;

public class OrganizationProfileRequestValidator : AbstractValidator<OrganizationProfileRequest>
{
    private const int MaxNameLength = 500;
    private const int MaxLineLength = 300;
    private const int MaxCodeLength = 30;

    public OrganizationProfileRequestValidator()
    {
        Line(x => x.FullName, "Полное наименование организации", MaxNameLength);
        Line(x => x.ShortName, "Краткое наименование организации", MaxNameLength);
        Line(x => x.Address, "Адрес");
        Line(x => x.Phone, "Телефон");
        Line(x => x.Fax, "Факс");
        Line(x => x.Email, "Электронная почта организации");
        Line(x => x.Website, "Сайт");

        Line(x => x.Okpo, "ОКПО", MaxCodeLength);
        Line(x => x.Ogrn, "ОГРН", MaxCodeLength);
        Line(x => x.Inn, "ИНН", MaxCodeLength);
        Line(x => x.Kpp, "КПП", MaxCodeLength);

        Line(x => x.SignerPosition, "Должность подписанта");
        Line(x => x.SignerName, "Подпись (ФИО)");

        Line(x => x.ContactName, "Контактное лицо (ФИО)");
        Line(x => x.ContactPosition, "Должность контактного лица");
        Line(x => x.ContactPhone, "Телефон контактного лица");
        Line(x => x.ContactEmail, "Электронная почта контактного лица");

        Line(x => x.ExecutorName, "Исполнитель (ФИО)");
        Line(x => x.ExecutorPhone, "Телефон исполнителя");
    }

    private void Line(
        Func<OrganizationProfileRequest, string?> selector,
        string label,
        int max = MaxLineLength)
    {
        RuleFor(request => selector(request))
            .MaximumLength(max)
            .WithName(label)
            .WithMessage($"Поле «{label}» длиннее {max} символов.");
    }
}
