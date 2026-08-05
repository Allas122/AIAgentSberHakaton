using ChatNode.Infrastructure.AI.Services.Abstractions;
using ChatNode.Infrastructure.Tools.Abstractions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ChatNode.Infrastructure.Tools;

public class DocxAnonymizer(IAnonymizeClient anonymizer) : IDocxAnonymizer
{
    public async Task<byte[]> AnonymizeAsync(Stream inputStream, string sessionId, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        await inputStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        using (var wordDoc = WordprocessingDocument.Open(memoryStream, true))
        {
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body is null) throw new InvalidOperationException("Документ не содержит тела — файл повреждён или это не docx.");

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                ct.ThrowIfCancellationRequested();

                var originalText = paragraph.InnerText;
                if (string.IsNullOrWhiteSpace(originalText)) continue;

                var anonymizedText = await anonymizer.AnonymizeAsync(originalText, sessionId);

                if (originalText == anonymizedText) continue;

                paragraph.RemoveAllChildren<Run>();
                paragraph.AppendChild(new Run(new Text(anonymizedText)));
            }

            wordDoc.MainDocumentPart!.Document.Save();
        }

        return memoryStream.ToArray();
    }
}
