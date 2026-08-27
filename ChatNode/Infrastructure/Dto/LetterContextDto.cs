namespace ChatNode.Infrastructure.Dto;

public record LetterOrganizationDto(
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
    string ContactEmail);

public record LetterAddresseeContextDto(string? Name, string? Position, string? Salutation);

public record LetterContextDto(LetterOrganizationDto? Organization, LetterAddresseeContextDto? Addressee)
{
    public static readonly LetterContextDto Empty = new(null, null);
}
