namespace ChatNode.Infrastructure.Configuration.Options;

public class ManualUploadOptions
{
    public long MaxFileSizeBytes { get; set; } = 256 * 1024;

    public long StaffMaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    public int MaxTextChars { get; set; } = 400_000;
}
