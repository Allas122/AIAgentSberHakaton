using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ChatNode.Infrastructure.Tools.Documents;

public static class XlsxTableParser
{
    private const int MaxColumns = 64;

    public static IReadOnlyList<SourceTable> Extract(Stream spreadsheetStream)
    {
        using var buffer = new MemoryStream();
        spreadsheetStream.CopyTo(buffer);
        buffer.Position = 0;

        using var document = SpreadsheetDocument.Open(buffer, false);

        var workbookPart = document.WorkbookPart;
        if (workbookPart?.Workbook is not { } workbook) return [];

        var sheets = workbook.Sheets?.Elements<Sheet>().ToList();
        if (sheets is null || sheets.Count == 0) return [];

        var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

        var sharedStrings = sharedStringTable is null
            ? []
            : sharedStringTable.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray();

        var tables = new List<SourceTable>();

        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is not { } relationshipId) continue;
            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart) continue;

            var rows = ReadRows(worksheetPart, sharedStrings);
            if (rows.Count == 0) continue;

            var table = TableFactory.FromRows(sheet.Name?.Value ?? "Лист", rows);
            if (table is not null) tables.Add(table);
        }

        return tables;
    }

    private static List<List<string>> ReadRows(WorksheetPart worksheetPart, string[] sharedStrings)
    {
        var rows = new List<List<string>>();

        if (worksheetPart.Worksheet is not { } worksheet) return rows;

        foreach (var row in worksheet.Descendants<Row>())
        {
            var cells = new List<string>();

            foreach (var cell in row.Elements<Cell>())
            {
                var index = ColumnIndex(cell.CellReference?.Value);

                while (index >= 0 && cells.Count < index && cells.Count < MaxColumns) cells.Add(string.Empty);

                if (cells.Count >= MaxColumns) break;

                cells.Add(CellText(cell, sharedStrings));
            }

            while (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[^1])) cells.RemoveAt(cells.Count - 1);

            if (cells.Count > 0) rows.Add(cells);
        }

        return rows;
    }

    private static string CellText(Cell cell, string[] sharedStrings)
    {
        var raw = cell.CellValue?.InnerText ?? cell.InnerText;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Length
                ? TableFactory.Normalize(sharedStrings[index])
                : string.Empty;
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return TableFactory.Normalize(cell.InlineString?.InnerText ?? string.Empty);
        }

        return TableFactory.Normalize(raw);
    }

    private static int ColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference)) return -1;

        var index = 0;

        foreach (var symbol in cellReference)
        {
            if (!char.IsLetter(symbol)) break;

            index = index * 26 + (char.ToUpperInvariant(symbol) - 'A' + 1);
        }

        return index - 1;
    }
}
