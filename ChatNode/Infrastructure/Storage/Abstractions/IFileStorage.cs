namespace ChatNode.Infrastructure.Storage.Abstractions;

public interface IFileStorage
{
    Task UploadAsync(string key, Stream content, CancellationToken ct = default);
    Task UploadAsync(string key, byte[] content, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
