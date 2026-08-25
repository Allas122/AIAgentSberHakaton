namespace ChatNode.Infrastructure.Storage;

public static class StorageKeys
{
    private const string ManualPrefix = "manuals/";
    private const string ManualSuffix = ".md";

    public static string Manual(Guid manualId) => $"{ManualPrefix}{manualId}{ManualSuffix}";

    public static string GrantApplication(Guid chatId, Guid applicationId) =>
        $"applications/{chatId}/{applicationId}.docx";

    public static bool TryParseManual(string key, out Guid manualId)
    {
        manualId = Guid.Empty;

        if (!key.StartsWith(ManualPrefix, StringComparison.Ordinal)) return false;
        if (!key.EndsWith(ManualSuffix, StringComparison.OrdinalIgnoreCase)) return false;

        var id = key[ManualPrefix.Length..^ManualSuffix.Length];

        return Guid.TryParse(id, out manualId);
    }
}
