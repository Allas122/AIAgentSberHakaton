namespace Domain.Entities;

public record ApplicationPart(
    Guid Id,
    Guid ApplicationId,
    string SectionTag,
    string FieldName,
    string Content,
    string Metadata
);