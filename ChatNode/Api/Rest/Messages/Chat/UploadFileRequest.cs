namespace ChatNode.Api.Rest.Messages.Chat;

public class UploadFileRequest
{
    public required Guid ManualId { get; set; }
    public string? Content { get; set; }
    public required IFormFile File { get; set; }
}
