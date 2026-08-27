using Domain.ValueTypes;

namespace ChatNode.Infrastructure.Tools;

public readonly record struct GenderGuess(PersonGender Gender, GenderSource Source)
{
    public static readonly GenderGuess Unknown = new(PersonGender.Unknown, GenderSource.None);

    public bool NeedsConfirmation => Source == GenderSource.Surname;
}

public static class RussianNameGender
{
    private static readonly string[] MalePatronymic = ["ович", "евич"];
    private static readonly string[] FemalePatronymic = ["овна", "евна", "ична"];

    private static readonly string[] MaleSurname =
        ["овский", "евский", "ский", "цкий", "ов", "ев", "ин", "ын", "ой", "ый", "ий"];

    private static readonly string[] FemaleSurname =
        ["овская", "евская", "ская", "цкая", "ова", "ева", "ина", "ына", "ая", "яя"];

    private static readonly string[] AmbiguousSurname =
    [
        "ых", "их",
        "ко",
        "ово", "аго",
        "ук", "юк",
        "ян", "янц", "унц",
        "дзе", "швили", "иа",
        "ич",
        "у", "ю", "е", "э", "и", "о"
    ];

    public static GenderGuess Detect(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return GenderGuess.Unknown;

        var tokens = Tokenize(fullName).Select(Normalize).ToList();

        if (tokens.Count == 0) return GenderGuess.Unknown;

        foreach (var token in tokens)
        {
            if (EndsWithAny(token, FemalePatronymic)) return new GenderGuess(PersonGender.Female, GenderSource.Patronymic);
            if (EndsWithAny(token, MalePatronymic)) return new GenderGuess(PersonGender.Male, GenderSource.Patronymic);
        }

        var verdicts = new HashSet<PersonGender>();

        foreach (var token in tokens)
        {
            if (EndsWithAny(token, AmbiguousSurname)) continue;

            if (EndsWithAny(token, FemaleSurname)) verdicts.Add(PersonGender.Female);
            else if (EndsWithAny(token, MaleSurname)) verdicts.Add(PersonGender.Male);
        }

        return verdicts.Count == 1
            ? new GenderGuess(verdicts.Single(), GenderSource.Surname)
            : GenderGuess.Unknown;
    }

    public static string? GivenAndPatronymic(string? fullName)
    {
        var tokens = Tokenize(fullName);

        for (var i = 1; i < tokens.Count; i++)
        {
            var normalized = Normalize(tokens[i]);

            if (!EndsWithAny(normalized, MalePatronymic) && !EndsWithAny(normalized, FemalePatronymic)) continue;

            return $"{tokens[i - 1]} {tokens[i]}";
        }

        return null;
    }

    public static string? Salutation(PersonGender gender) => gender switch
    {
        PersonGender.Male => "Уважаемый",
        PersonGender.Female => "Уважаемая",
        _ => null
    };

    private static List<string> Tokenize(string? fullName) =>
        string.IsNullOrWhiteSpace(fullName)
            ? []
            : fullName
                .Replace(',', ' ')
                .Replace(';', ' ')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Length > 2 && !token.Contains('.'))
                .ToList();

    private static string Normalize(string token) => token.Replace("ё", "е").ToLowerInvariant();

    private static bool EndsWithAny(string token, IEnumerable<string> suffixes) =>
        suffixes.Any(suffix => token.EndsWith(suffix, StringComparison.Ordinal));
}
