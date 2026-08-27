using ChatNode.Infrastructure.Tools.Documents;

namespace ChatNode.Infrastructure.Tools.Abstractions;

public record KnowledgeDocument(string Markdown, IReadOnlyList<SourceTable> Tables)
{
    public static readonly KnowledgeDocument Empty = new(string.Empty, []);
}

public interface IKnowledgeDocumentReader
{
    IReadOnlyList<string> SupportedExtensions { get; }

    bool Supports(string? fileName);

    KnowledgeDocument Read(Stream fileStream, string? fileName);
}
