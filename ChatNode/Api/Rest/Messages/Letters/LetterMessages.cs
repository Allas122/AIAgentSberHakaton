namespace ChatNode.Api.Rest.Messages.Letters;

public class ComposeLetterRequest
{
    public IFormFile? File { get; set; }
    public string? Text { get; set; }
    public string? Intent { get; set; }
    public Guid? TemplateId { get; set; }
}

public class CreateLetterTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
    public IFormFile? File { get; set; }
}

public record UpdateLetterTemplateRequest(string? Name, string? Content);

public record LetterTemplateResponse(
    Guid Id,
    string Name,
    string Content,
    string? SourceFileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ComposedAssignment(Guid Id, string Title, string? Assignee, string? DueDate, string Status);

public record ComposeLetterResponse(string Reply, IReadOnlyList<ComposedAssignment> Assignments);
