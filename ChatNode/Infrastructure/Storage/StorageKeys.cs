namespace ChatNode.Infrastructure.Storage;

public static class StorageKeys
{
    public static string Manual(Guid manualId) => $"manuals/{manualId}.md";

    public static string GrantApplication(Guid chatId, Guid applicationId) =>
        $"applications/{chatId}/{applicationId}.docx";
}
