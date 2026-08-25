namespace ChatNode.Api.Rest.Messages.Chat;

public record UploadFileResponse(
    string MessageId,
    Guid ApplicationId,
    Guid? DocumentId,
    string FileName,
    long QueueDepth);
