using Domain.ValueTypes;

namespace ChatNode.Api.Rest.Messages.Manual;

public class UploadManualRequest
{
    public required string Title { get; set; }
    public required IFormFile File { get; set; }
    public ManualScope? Scope { get; set; }
}
