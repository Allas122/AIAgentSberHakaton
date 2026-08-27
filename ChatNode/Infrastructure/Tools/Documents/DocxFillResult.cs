namespace ChatNode.Infrastructure.Tools.Documents;

public record DocxFillResult(
    byte[] Content,
    IReadOnlyList<string> Filled,
    IReadOnlyList<string> LeftEmpty,
    IReadOnlyList<string> Unknown);
