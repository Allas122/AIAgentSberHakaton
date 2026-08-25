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
            var document = wordDoc.MainDocumentPart?.Document;
            var body = document?.Body;

            if (document is null || body is null)
            {
                throw new InvalidOperationException("Документ не содержит тела — файл повреждён или это не docx.");
            }

            var paragraphs = body.Descendants<Paragraph>()
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph.InnerText))
                .ToList();

            if (paragraphs.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var originals = paragraphs.Select(paragraph => paragraph.InnerText).ToList();

                var anonymized = await anonymizer.AnonymizeBatchAsync(originals, sessionId, ct);

                for (var i = 0; i < paragraphs.Count; i++)
                {
                    if (originals[i] == anonymized[i]) continue;

                    paragraphs[i].RemoveAllChildren<Run>();
                    paragraphs[i].AppendChild(new Run(new Text(anonymized[i])));
                }
            }

            document.Save();
        }

        return memoryStream.ToArray();
    }
}
