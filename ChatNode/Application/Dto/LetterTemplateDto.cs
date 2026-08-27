namespace ChatNode.Application.DTO;

public record LetterTemplateDto(
    Guid Id,
    string Name,
    string Content,
    string? SourceFileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPreset = false,
    bool HasForm = false,
    IReadOnlyList<string>? Placeholders = null);

public record CreateLetterTemplateDto(string Name, string? Content, Stream? FileStream, string? FileName);

public record UpdateLetterTemplateDto(string? Name, string? Content);