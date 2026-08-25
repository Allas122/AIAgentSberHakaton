namespace ChatNode.Api.Rest.Messages.Manual;

public class UploadManualRequest
{
    public required string Title { get; set; }
    public required IFormFile File { get; set; }
}
