namespace ChatNode.Infrastructure.Storage;

public static class StorageKeys
{
    private const string ManualPrefix = "manuals/";
    private const string ManualSuffix = ".md";

    public static string Manual(Guid manualId) => $"{ManualPrefix}{manualId}{ManualSuffix}";

    public static string GrantApplication(Guid chatId, Guid applicationId, string? fileName = null) =>
        $"applications/{chatId}/{applicationId}{ApplicationSuffix(fileName)}";

    public static string LetterForm(Guid ownerId, Guid templateId) =>
        $"letter-forms/{ownerId}/{templateId}.docx";

    public static string GeneratedLetter(Guid ownerId, Guid letterId) =>
        $"letters/{ownerId}/{letterId}.docx";

    private static string ApplicationSuffix(string? fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        return extension is ".pdf" ? extension : ".docx";
    }

    public static bool TryParseManual(string key, out Guid manualId)
    {
        manualId = Guid.Empty;

        if (!key.StartsWith(ManualPrefix, StringComparison.Ordinal)) return false;
        if (!key.EndsWith(ManualSuffix, StringComparison.OrdinalIgnoreCase)) return false;

        var id = key[ManualPrefix.Length..^ManualSuffix.Length];

        return Guid.TryParse(id, out manualId);
    }
}
