using ChatNode.Application.DTO;
using ChatNode.Infrastructure.Tools.Documents;

namespace ChatNode.Application.Services;

public static class LetterFormValues
{
    public static IReadOnlyDictionary<string, string?> Build(
        OrganizationProfileDto profile,
        LetterAddresseeDto? addressee,
        string? salutation,
        LetterRequisitesDto requisites,
        string body) =>
        new Dictionary<string, string?>
        {
            [LetterPlaceholders.Body] = body,
            [LetterPlaceholders.Addressee] = Addressee(addressee),
            [LetterPlaceholders.Salutation] = salutation,
            [LetterPlaceholders.Signature] = Join(" ", profile.SignerPosition, profile.SignerName),
            [LetterPlaceholders.SignerPosition] = profile.SignerPosition,
            [LetterPlaceholders.SignerName] = profile.SignerName,
            [LetterPlaceholders.Contact] = Contact(profile),
            [LetterPlaceholders.ExecutorName] = profile.ExecutorName,
            [LetterPlaceholders.ExecutorPhone] = profile.ExecutorPhone,
            [LetterPlaceholders.OutgoingNumber] = requisites.OutgoingNumber,
            [LetterPlaceholders.OutgoingDate] = requisites.OutgoingDate,
            [LetterPlaceholders.ReplyToNumber] = requisites.ReplyToNumber,
            [LetterPlaceholders.ReplyToDate] = requisites.ReplyToDate,
            [LetterPlaceholders.FullName] = profile.FullName,
            [LetterPlaceholders.ShortName] = profile.ShortName,
            [LetterPlaceholders.Address] = profile.Address,
            [LetterPlaceholders.Phone] = profile.Phone,
            [LetterPlaceholders.Fax] = profile.Fax,
            [LetterPlaceholders.Email] = profile.Email,
            [LetterPlaceholders.Website] = profile.Website,
            [LetterPlaceholders.Okpo] = profile.Okpo,
            [LetterPlaceholders.Ogrn] = profile.Ogrn,
            [LetterPlaceholders.Inn] = profile.Inn,
            [LetterPlaceholders.Kpp] = profile.Kpp
        };

    private static string? Addressee(LetterAddresseeDto? addressee)
    {
        if (addressee is null) return null;

        return Join("\n", addressee.Position, addressee.Name);
    }

    private static string? Contact(OrganizationProfileDto profile)
    {
        if (!profile.HasContact) return null;

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(profile.ContactName)) parts.Add(profile.ContactName.Trim());
        if (!string.IsNullOrWhiteSpace(profile.ContactPosition)) parts.Add(profile.ContactPosition.Trim());
        if (!string.IsNullOrWhiteSpace(profile.ContactPhone)) parts.Add($"тел. {profile.ContactPhone.Trim()}");
        if (!string.IsNullOrWhiteSpace(profile.ContactEmail)) parts.Add($"эл. почта: {profile.ContactEmail.Trim()}");

        return parts.Count == 0 ? null : "Контактное лицо — " + string.Join(", ", parts);
    }

    private static string? Join(string separator, params string?[] parts)
    {
        var filled = parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).ToList();

        return filled.Count == 0 ? null : string.Join(separator, filled);
    }
}
