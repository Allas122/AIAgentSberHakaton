using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using ChatNode.Infrastructure.Configuration.Options;
using ChatNode.Infrastructure.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace ChatNode.Infrastructure.Storage;

public class S3FileStorage(IAmazonS3 s3Client, IOptions<S3Options> options) : IFileStorage
{
    private const string UnsignedPayloadHeader = "x-amz-content-sha256";
    private const string UnsignedPayload = "UNSIGNED-PAYLOAD";

    private readonly S3Options _options = options.Value;

    public async Task UploadAsync(string key, Stream content, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content
        };
        request.Headers[UnsignedPayloadHeader] = UnsignedPayload;

        await s3Client.PutObjectAsync(request, ct);
    }

    public async Task UploadAsync(string key, byte[] content, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content, writable: false);
        await UploadAsync(key, stream, ct);
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var response = await s3Client.GetObjectAsync(_options.BucketName, key, ct);

            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            return buffer;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Объект \"{key}\" не найден в хранилище.", key, ex);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(_options.BucketName, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await s3Client.DeleteObjectAsync(_options.BucketName, key, ct);
    }
}
