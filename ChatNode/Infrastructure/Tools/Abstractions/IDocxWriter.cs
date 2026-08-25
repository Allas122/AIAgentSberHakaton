using ChatNode.Infrastructure.Tools.Documents;

namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IDocxWriter
{
    byte[] Write(DocumentModel document);
}
