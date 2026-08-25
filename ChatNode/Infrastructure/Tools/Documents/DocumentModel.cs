namespace ChatNode.Infrastructure.Tools.Documents;

public abstract record DocumentBlock;

public sealed record HeadingBlock(int Level, string Text) : DocumentBlock;

public sealed record ParagraphBlock(string Text) : DocumentBlock;

public sealed record BulletsBlock(IReadOnlyList<string> Items, bool Numbered = false) : DocumentBlock;

public sealed record TableBlock(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows)
    : DocumentBlock;

public sealed record PageBreakBlock : DocumentBlock;

public sealed record DocumentSignature(string Position, string Name, string? ExecutorLine = null);

public sealed record DocumentHeader(
    string? Organization = null,
    bool ShowOutgoingNumber = true,
    DateTimeOffset? Date = null);

public sealed record DocumentModel(
    string Title,
    string? Subtitle,
    IReadOnlyList<DocumentBlock> Blocks,
    DocumentSignature? Signature = null,
    DocumentHeader? Header = null);
