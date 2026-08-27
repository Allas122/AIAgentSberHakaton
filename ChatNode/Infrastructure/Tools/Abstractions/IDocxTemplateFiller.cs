using ChatNode.Infrastructure.Tools.Documents;

namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IDocxTemplateFiller
{
    DocxFillResult Fill(byte[] form, IReadOnlyDictionary<string, string?> values);

    IReadOnlyList<string> Scan(byte[] form);
}
