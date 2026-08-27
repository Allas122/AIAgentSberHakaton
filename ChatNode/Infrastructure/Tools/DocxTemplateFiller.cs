using System.Text;
using System.Text.RegularExpressions;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Tools.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ChatNode.Infrastructure.Tools;

public class DocxTemplateFiller : IDocxTemplateFiller
{
    public IReadOnlyList<string> Scan(byte[] form)
    {
        using var stream = Editable(form);
        using var package = WordprocessingDocument.Open(stream, false);

        var found = new List<string>();

        foreach (var root in Parts(package))
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                foreach (Match match in LetterPlaceholders.Token.Matches(Joined(Texts(paragraph))))
                {
                    var key = LetterPlaceholders.Normalize(match.Groups[1].Value);

                    if (!found.Contains(key)) found.Add(key);
                }
            }
        }

        return found;
    }

    public DocxFillResult Fill(byte[] form, IReadOnlyDictionary<string, string?> values)
    {
        var filled = new List<string>();
        var leftEmpty = new List<string>();
        var unknown = new List<string>();

        using var stream = Editable(form);

        using (var package = WordprocessingDocument.Open(stream, true))
        {
            foreach (var root in Parts(package))
            {
                foreach (var paragraph in root.Descendants<Paragraph>().ToList())
                {
                    FillParagraph(paragraph, values, filled, leftEmpty, unknown);
                }

                root.Save();
            }
        }

        return new DocxFillResult(stream.ToArray(), filled, leftEmpty, unknown);
    }

    private static void FillParagraph(
        Paragraph paragraph,
        IReadOnlyDictionary<string, string?> values,
        List<string> filled,
        List<string> leftEmpty,
        List<string> unknown)
    {
        var texts = Texts(paragraph);
        if (texts.Count == 0) return;

        var joined = Joined(texts);

        var matches = LetterPlaceholders.Token.Matches(joined);
        if (matches.Count == 0) return;

        var starts = Offsets(texts);

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var key = LetterPlaceholders.Normalize(match.Groups[1].Value);

            if (!LetterPlaceholders.IsKnown(key))
            {
                if (!unknown.Contains(key)) unknown.Add(key);
                continue;
            }

            var value = values.GetValueOrDefault(key);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (!leftEmpty.Contains(key)) leftEmpty.Add(key);
                value = LetterPlaceholders.Fallback(key);
            }
            else if (!filled.Contains(key))
            {
                filled.Add(key);
            }

            Substitute(paragraph, texts, starts, match, value, joined);
        }
    }

    private static void Substitute(
        Paragraph paragraph,
        IReadOnlyList<Text> texts,
        IReadOnlyList<int> starts,
        Match match,
        string value,
        string joined)
    {
        var (firstIndex, firstOffset) = Locate(starts, texts, match.Index);
        var (lastIndex, lastOffset) = Locate(starts, texts, match.Index + match.Length);

        var prefix = texts[firstIndex].Text[..firstOffset];
        var suffix = texts[lastIndex].Text[lastOffset..];

        var lines = value.Replace("\r\n", "\n").Split('\n');
        var alone = joined.Trim() == match.Value;

        for (var i = firstIndex + 1; i <= lastIndex; i++) SetText(texts[i], string.Empty);

        var head = lines[0];

        SetText(texts[firstIndex], firstIndex == lastIndex ? prefix + head + suffix : prefix + head);
        if (firstIndex != lastIndex) SetText(texts[lastIndex], suffix);

        if (lines.Length == 1) return;

        var host = texts[firstIndex].Ancestors<Run>().FirstOrDefault();

        if (alone && host is not null)
        {
            AppendParagraphs(paragraph, host, lines.Skip(1));
            return;
        }

        AppendBreaks(texts[firstIndex], lines.Skip(1));
    }

    private static void AppendParagraphs(Paragraph paragraph, Run host, IEnumerable<string> lines)
    {
        if (paragraph.Parent is not { } parent) return;

        var properties = (ParagraphProperties?)paragraph.ParagraphProperties?.CloneNode(true);
        properties?.RemoveAllChildren<SectionProperties>();

        var runProperties = Clean((RunProperties?)host.RunProperties?.CloneNode(true));

        OpenXmlElement anchor = paragraph;

        foreach (var line in lines)
        {
            var run = new Run();
            if (runProperties is not null) run.AppendChild(runProperties.CloneNode(true));
            run.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });

            var next = new Paragraph();
            if (properties is not null) next.AppendChild(properties.CloneNode(true));
            next.AppendChild(run);

            anchor = parent.InsertAfter(next, anchor);
        }
    }

    private static void AppendBreaks(Text anchor, IEnumerable<string> lines)
    {
        if (anchor.Parent is not Run run) return;

        OpenXmlElement previous = anchor;

        foreach (var line in lines)
        {
            previous = run.InsertAfter(new Break(), previous);
            previous = run.InsertAfter(new Text(line) { Space = SpaceProcessingModeValues.Preserve }, previous);
        }
    }

    private static RunProperties? Clean(RunProperties? properties)
    {
        properties?.RemoveAllChildren<Highlight>();

        return properties;
    }

    private static (int Index, int Offset) Locate(IReadOnlyList<int> starts, IReadOnlyList<Text> texts, int position)
    {
        for (var i = texts.Count - 1; i >= 0; i--)
        {
            if (position >= starts[i]) return (i, position - starts[i]);
        }

        return (0, 0);
    }

    private static void SetText(Text text, string value)
    {
        text.Text = value;
        text.Space = SpaceProcessingModeValues.Preserve;
    }

    private static List<Text> Texts(Paragraph paragraph) => paragraph.Descendants<Text>().ToList();

    private static string Joined(IReadOnlyList<Text> texts)
    {
        var builder = new StringBuilder();

        foreach (var text in texts) builder.Append(text.Text);

        return builder.ToString();
    }

    private static List<int> Offsets(IReadOnlyList<Text> texts)
    {
        var starts = new List<int>(texts.Count);
        var position = 0;

        foreach (var text in texts)
        {
            starts.Add(position);
            position += text.Text.Length;
        }

        return starts;
    }

    private static IEnumerable<OpenXmlPartRootElement> Parts(WordprocessingDocument package)
    {
        var main = package.MainDocumentPart
                   ?? throw new InvalidOperationException("Бланк повреждён: в файле нет основной части документа.");

        if (main.Document is not null) yield return main.Document;

        foreach (var header in main.HeaderParts)
        {
            if (header.Header is not null) yield return header.Header;
        }

        foreach (var footer in main.FooterParts)
        {
            if (footer.Footer is not null) yield return footer.Footer;
        }
    }

    private static MemoryStream Editable(byte[] form)
    {
        var stream = new MemoryStream();

        stream.Write(form, 0, form.Length);
        stream.Position = 0;

        return stream;
    }
}
