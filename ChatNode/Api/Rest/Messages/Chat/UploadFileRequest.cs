using Domain.ValueTypes;
namespace ChatNode.Api.Rest.Messages.Chat;

public class UploadFileRequest
{
    public required Guid ManualId { get; set; }
    public string? Content { get; set; }
    public ContestKind ContestKind { get; set; } = ContestKind.Individual;
    public required IFormFile File { get; set; }
}
