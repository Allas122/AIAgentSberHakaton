using System.Text.RegularExpressions;

namespace ChatNode.Infrastructure.Tools.Documents;

public enum PlaceholderFallback
{
    Blank,
    Underscores
}

public static partial class LetterPlaceholders
{
    public const string Body = "ТЕЛО";
    public const string Addressee = "АДРЕСАТ";
    public const string Salutation = "ОБРАЩЕНИЕ";
    public const string Signature = "ПОДПИСЬ";
    public const string SignerPosition = "ПОДПИСЬ ДОЛЖНОСТЬ";
    public const string SignerName = "ПОДПИСЬ ФИО";
    public const string Contact = "КОНТАКТНОЕ ЛИЦО";
    public const string ExecutorName = "ИСПОЛНИТЕЛЬ ФИО";
    public const string ExecutorPhone = "ИСПОЛНИТЕЛЬ ТЕЛЕФОН";
    public const string OutgoingNumber = "ИСХ НОМЕР";
    public const string OutgoingDate = "ИСХ ДАТА";
    public const string ReplyToNumber = "НА НОМЕР";
    public const string ReplyToDate = "НА ДАТА";
    public const string FullName = "ПОЛНОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ";
    public const string ShortName = "КРАТКОЕ НАИМЕНОВАНИЕ ОРГАНИЗАЦИИ";
    public const string Address = "АДРЕС";
    public const string Phone = "ТЕЛЕФОН";
    public const string Fax = "ФАКС";
    public const string Email = "EMAIL";
    public const string Website = "САЙТ";
    public const string Okpo = "ОКПО";
    public const string Ogrn = "ОГРН";
    public const string Inn = "ИНН";
    public const string Kpp = "КПП";

    private const string Dashes = "__________";

    private static readonly Dictionary<string, PlaceholderFallback> Known = new()
    {
        [Body] = PlaceholderFallback.Blank,
        [Addressee] = PlaceholderFallback.Blank,
        [Salutation] = PlaceholderFallback.Blank,
        [Signature] = PlaceholderFallback.Blank,
        [SignerPosition] = PlaceholderFallback.Blank,
        [SignerName] = PlaceholderFallback.Blank,
        [Contact] = PlaceholderFallback.Blank,
        [ExecutorName] = PlaceholderFallback.Blank,
        [ExecutorPhone] = PlaceholderFallback.Blank,
        [OutgoingNumber] = PlaceholderFallback.Underscores,
        [OutgoingDate] = PlaceholderFallback.Underscores,
        [ReplyToNumber] = PlaceholderFallback.Underscores,
        [ReplyToDate] = PlaceholderFallback.Underscores,
        [FullName] = PlaceholderFallback.Blank,
        [ShortName] = PlaceholderFallback.Blank,
        [Address] = PlaceholderFallback.Blank,
        [Phone] = PlaceholderFallback.Blank,
        [Fax] = PlaceholderFallback.Blank,
        [Email] = PlaceholderFallback.Blank,
        [Website] = PlaceholderFallback.Blank,
        [Okpo] = PlaceholderFallback.Blank,
        [Ogrn] = PlaceholderFallback.Blank,
        [Inn] = PlaceholderFallback.Blank,
        [Kpp] = PlaceholderFallback.Blank
    };

    [GeneratedRegex(@"<\s*([\p{L}][\p{L}0-9_ .\-]{0,60}?)\s*>", RegexOptions.None, matchTimeoutMilliseconds: 500)]
    public static partial Regex Token { get; }

    public static IReadOnlyCollection<string> All => Known.Keys;

    public static string Normalize(string raw) =>
        string.Join(' ', raw.Replace('_', ' ').ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static bool IsKnown(string key) => Known.ContainsKey(key);

    public static string Fallback(string key) =>
        Known.TryGetValue(key, out var behavior) && behavior == PlaceholderFallback.Underscores
            ? Dashes
            : string.Empty;
}
