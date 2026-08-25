using System.Globalization;
using ChatNode.Infrastructure.Tools.Abstractions;
using ChatNode.Infrastructure.Tools.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ChatNode.Infrastructure.Tools;

public class DocxWriter : IDocxWriter
{
    private const string FontName = "Times New Roman";

    private const int BodyHalfPoints = 28;
    private const int TitleHalfPoints = 32;
    private const int SmallHalfPoints = 24;

    private const int MarginTop = 1134;
    private const int MarginRight = 567;
    private const int MarginBottom = 1134;
    private const int MarginLeft = 1701;

    private const int LineSpacing = 360;
    private const int ParagraphSpacingAfter = 120;
    private const int HeadingSpacingBefore = 240;

    private static readonly int[] HeadingSizes = [30, 28, 28, 28];

    public byte[] Write(DocumentModel document)
    {
        using var stream = new MemoryStream();

        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = package.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            AppendHeader(body, document.Header);

            body.AppendChild(Title(document.Title));

            if (!string.IsNullOrWhiteSpace(document.Subtitle))
            {
                body.AppendChild(Subtitle(document.Subtitle));
            }

            foreach (var block in document.Blocks) AppendBlock(body, block);

            AppendSignature(body, document.Signature);

            body.AppendChild(PageSettings());

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void AppendHeader(Body body, DocumentHeader? header)
    {
        if (header is null) return;

        if (!string.IsNullOrWhiteSpace(header.Organization))
        {
            body.AppendChild(Line(header.Organization, JustificationValues.Center, bold: true));
        }

        var date = (header.Date ?? DateTimeOffset.Now).ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));

        var stamp = header.ShowOutgoingNumber
            ? $"№ ______________ от {date}"
            : date;

        body.AppendChild(Line(stamp, JustificationValues.Right, size: SmallHalfPoints));
        body.AppendChild(Spacer());
    }

    private static void AppendBlock(Body body, DocumentBlock block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                body.AppendChild(Heading(heading));
                break;

            case ParagraphBlock paragraph:
                body.AppendChild(Line(paragraph.Text, JustificationValues.Both));
                break;

            case BulletsBlock bullets:
                var index = 1;
                foreach (var item in bullets.Items)
                {
                    var marker = bullets.Numbered ? $"{index++}. " : "— ";
                    body.AppendChild(Bullet(marker + item));
                }

                break;

            case TableBlock table:
                body.AppendChild(BuildTable(table));
                body.AppendChild(Spacer());
                break;

            case PageBreakBlock:
                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                break;
        }
    }

    private static void AppendSignature(Body body, DocumentSignature? signature)
    {
        if (signature is null) return;

        body.AppendChild(Spacer());

        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));

        table.AppendChild(Grid(3));

        var row = new TableRow();
        row.AppendChild(Cell(signature.Position, JustificationValues.Left, "3000"));
        row.AppendChild(Cell("______________", JustificationValues.Center, "2000"));
        row.AppendChild(Cell(signature.Name, JustificationValues.Right, "3000"));
        table.AppendChild(row);

        body.AppendChild(table);

        if (!string.IsNullOrWhiteSpace(signature.ExecutorLine))
        {
            body.AppendChild(Spacer());
            body.AppendChild(Line(signature.ExecutorLine, JustificationValues.Left, size: SmallHalfPoints));
        }
    }

    private static Table BuildTable(TableBlock block)
    {
        var table = new Table();

        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        var columns = Math.Max(block.Header.Count, block.Rows.Count == 0 ? 0 : block.Rows.Max(row => row.Count));
        table.AppendChild(Grid(columns));

        if (block.Header.Count > 0)
        {
            var header = new TableRow();
            foreach (var cell in block.Header) header.AppendChild(Cell(cell, JustificationValues.Left, null, bold: true));
            table.AppendChild(header);
        }

        foreach (var row in block.Rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row) tableRow.AppendChild(Cell(cell, JustificationValues.Left, null));
            table.AppendChild(tableRow);
        }

        return table;
    }

    private static TableGrid Grid(int columns)
    {
        var grid = new TableGrid();
        for (var i = 0; i < Math.Max(columns, 1); i++) grid.AppendChild(new GridColumn());
        return grid;
    }

    private static TableCell Cell(string text, JustificationValues alignment, string? width, bool bold = false)
    {
        var cell = new TableCell();

        if (width is not null)
        {
            cell.AppendChild(new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Pct }));
        }

        cell.AppendChild(Line(text, alignment, bold: bold, spacingAfter: 0));
        return cell;
    }

    private static Paragraph Title(string text) =>
        Line(text, JustificationValues.Center, bold: true, size: TitleHalfPoints, spacingBefore: HeadingSpacingBefore);

    private static Paragraph Subtitle(string text) =>
        Line(text, JustificationValues.Center, size: SmallHalfPoints);

    private static Paragraph Heading(HeadingBlock heading)
    {
        var level = Math.Clamp(heading.Level, 1, HeadingSizes.Length);
        var size = HeadingSizes[level - 1];

        return Line(
            heading.Text,
            JustificationValues.Left,
            bold: true,
            size: size,
            spacingBefore: HeadingSpacingBefore);
    }

    private static Paragraph Bullet(string text) =>
        Line(text, JustificationValues.Both, indentation: new Indentation { Left = "426", Hanging = "284" });

    private static Paragraph Spacer() => Line(string.Empty, JustificationValues.Left);

    private static Paragraph Line(
        string text,
        JustificationValues alignment,
        bool bold = false,
        int size = BodyHalfPoints,
        int spacingBefore = 0,
        int spacingAfter = ParagraphSpacingAfter,
        Indentation? indentation = null)
    {
        var runProperties = new RunProperties();
        runProperties.AppendChild(new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName });

        if (bold) runProperties.AppendChild(new Bold());

        runProperties.AppendChild(new FontSize { Val = size.ToString() });
        runProperties.AppendChild(new FontSizeComplexScript { Val = size.ToString() });

        var run = new Run(runProperties);

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.AppendChild(new Break());
            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
        }

        var paragraphProperties = new ParagraphProperties();

        paragraphProperties.AppendChild(new SpacingBetweenLines
        {
            Line = LineSpacing.ToString(),
            LineRule = LineSpacingRuleValues.Auto,
            Before = spacingBefore.ToString(),
            After = spacingAfter.ToString()
        });

        if (indentation is not null) paragraphProperties.AppendChild(indentation);

        paragraphProperties.AppendChild(new Justification { Val = alignment });

        return new Paragraph(paragraphProperties, run);
    }

    private static SectionProperties PageSettings() =>
        new(new PageSize { Width = 11906, Height = 16838 },
            new PageMargin
            {
                Top = MarginTop,
                Right = MarginRight,
                Bottom = MarginBottom,
                Left = MarginLeft,
                Header = 567,
                Footer = 567,
                Gutter = 0
            });
}
