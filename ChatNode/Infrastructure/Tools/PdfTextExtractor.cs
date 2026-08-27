using ChatNode.Infrastructure.Tools.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ChatNode.Infrastructure.Tools;

public class PdfTextExtractor : IPdfTextExtractor
{
    public IReadOnlyList<string> ExtractLines(Stream pdfStream)
    {
        using var buffer = new MemoryStream();
        pdfStream.CopyTo(buffer);
        buffer.Position = 0;

        using var document = PdfDocument.Open(buffer);

        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);

            if (string.IsNullOrWhiteSpace(text)) continue;

            foreach (var raw in text.Split('\n'))
            {
                var line = Normalize(raw);
                if (line.Length > 0) lines.Add(line);
            }
        }

        return lines;
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
