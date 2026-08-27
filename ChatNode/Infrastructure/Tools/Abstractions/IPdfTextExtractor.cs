namespace ChatNode.Infrastructure.Tools.Abstractions;

public interface IPdfTextExtractor
{
    IReadOnlyList<string> ExtractLines(Stream pdfStream);
}
