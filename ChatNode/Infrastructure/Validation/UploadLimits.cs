namespace ChatNode.Infrastructure.Validation;

public static class UploadLimits
{
    public const long MaxDocxBytes = 50L * 1024 * 1024;

    public const long MaxRequestBytes = 64L * 1024 * 1024;
}
