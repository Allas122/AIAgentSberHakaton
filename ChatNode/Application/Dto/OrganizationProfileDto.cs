using Domain.ValueTypes;

namespace ChatNode.Application.DTO;

public record OrganizationProfileDto(
    string FullName,
    string ShortName,
    string Address,
    string Phone,
    string Fax,
    string Email,
    string Website,
    string Okpo,
    string Ogrn,
    string Inn,
    string Kpp,
    string SignerPosition,
    string SignerName,
    string ContactName,
    string ContactPosition,
    string ContactPhone,
    string ContactEmail,
    string ExecutorName,
    string ExecutorPhone,
    DateTimeOffset? UpdatedAt)
{
    public static OrganizationProfileDto Empty { get; } = new(
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty,
        string.Empty, string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty,
        null);

    public bool HasSigner => !string.IsNullOrWhiteSpace(SignerName);

    public bool HasContact =>
        !string.IsNullOrWhiteSpace(ContactName) ||
        !string.IsNullOrWhiteSpace(ContactEmail) ||
        !string.IsNullOrWhiteSpace(ContactPhone);

    public bool IsEmpty => !HasSigner && !HasContact && string.IsNullOrWhiteSpace(FullName);

    public OrganizationProfileDto Trimmed() => this with
    {
        FullName = Clean(FullName),
        ShortName = Clean(ShortName),
        Address = Clean(Address),
        Phone = Clean(Phone),
        Fax = Clean(Fax),
        Email = Clean(Email),
        Website = Clean(Website),
        Okpo = Clean(Okpo),
        Ogrn = Clean(Ogrn),
        Inn = Clean(Inn),
        Kpp = Clean(Kpp),
        SignerPosition = Clean(SignerPosition),
        SignerName = Clean(SignerName),
        ContactName = Clean(ContactName),
        ContactPosition = Clean(ContactPosition),
        ContactPhone = Clean(ContactPhone),
        ContactEmail = Clean(ContactEmail),
        ExecutorName = Clean(ExecutorName),
        ExecutorPhone = Clean(ExecutorPhone)
    };

    private static string Clean(string? value) => (value ?? string.Empty).Trim();
}

public record LetterAddresseeDto(
    string? Name,
    string? Position,
    string? Salutation,
    PersonGender? Gender);

public record LetterRequisitesDto(
    string? OutgoingNumber,
    string? OutgoingDate,
    string? ReplyToNumber,
    string? ReplyToDate)
{
    public static readonly LetterRequisitesDto Empty = new(null, null, null, null);
}

public record ComposeLetterDto(
    Guid UserId,
    Stream? FileStream,
    string? FileName,
    string? LetterText,
    string? Intent,
    Guid? TemplateId,
    LetterAddresseeDto? Addressee,
    LetterRequisitesDto Requisites);
