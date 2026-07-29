namespace ChatNode.Infrastructure.Configuration.Options;

public class S3Options
{
    public required string ServiceUrl { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public required string BucketName { get; set; }
    public bool ForcePathStyle { get; set; }
    public bool UseHttp { get; set; }
}