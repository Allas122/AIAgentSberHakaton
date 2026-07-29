namespace ChatNode.Api.Rest.Messages.Chat;

public class UploadFileRequest
{
    public required string? Content { get; set; }
    public required IFormFile File { get; set; }
}