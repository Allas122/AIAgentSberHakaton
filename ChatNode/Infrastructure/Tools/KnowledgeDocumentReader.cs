using System.Text;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Tools.Documents;

namespace ChatNode.Infrastructure.Tools;

public class KnowledgeDocumentReader(
    IDocxTextExtractor docxTextExtractor,
    IPdfTextExtractor pdfTextExtractor) : IKnowledgeDocumentReader
{
    private const string Markdown = ".md";
    private const string Text = ".txt";
    private const string Docx = ".docx";
    private const string Pdf = ".pdf";
    private const string Csv = ".csv";
    private const string Xlsx = ".xlsx";

    public IReadOnlyList<string> SupportedExtensions { get; } = [Markdown, Text, Docx, Pdf, Csv, Xlsx];

    public bool Supports(string? fileName) =>
        SupportedExtensions.Contains(Extension(fileName), StringComparer.OrdinalIgnoreCase);

    public KnowledgeDocument Read(Stream fileStream, string? fileName)
    {
        switch (Extension(fileName))
        {
            case Xlsx:
                return FromTables(XlsxTableParser.Extract(fileStream));

            case Csv:
                var csv = CsvTableParser.Parse(
                    ReadAllText(fileStream),
                    Path.GetFileNameWithoutExtension(fileName ?? string.Empty));

                return csv is null ? KnowledgeDocument.Empty : FromTables([csv]);

            case Docx:
                return new KnowledgeDocument(docxTextExtractor.ExtractMarkdown(fileStream), []);

            case Pdf:
                return new KnowledgeDocument(string.Join("\n\n", pdfTextExtractor.ExtractLines(fileStream)), []);

            default:
                return new KnowledgeDocument(ReadAllText(fileStream), []);
        }
    }

    private static KnowledgeDocument FromTables(IReadOnlyList<SourceTable> tables) =>
        new(TableMarkdown.Render(tables), tables);

    private static string Extension(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

    private static string ReadAllText(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }
}
