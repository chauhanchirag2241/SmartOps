using ClosedXML.Excel;
using SmartOps.Application.Common.Excel;

namespace SmartOps.Infrastructure.Common.Excel;

public sealed class ExcelHelper : IExcelHelper
{
    private static readonly XLColor RequiredFill = XLColor.FromHtml("#FFCDD2");
    private static readonly XLColor RequiredFont = XLColor.FromHtml("#B71C1C");
    private static readonly XLColor OptionalFill = XLColor.FromHtml("#C8E6C9");
    private static readonly XLColor OptionalFont = XLColor.FromHtml("#1B5E20");
    private static readonly XLColor BannerFill = XLColor.FromHtml("#0D47A1");
    private static readonly XLColor LegendFill = XLColor.FromHtml("#FFF8E1");
    private static readonly XLColor NoteInfoFill = XLColor.FromHtml("#E3F2FD");
    private static readonly XLColor NoteTipFill = XLColor.FromHtml("#E8F5E9");
    private static readonly XLColor NoteWarnFill = XLColor.FromHtml("#FFF3E0");
    private static readonly XLColor NoteRequiredFill = XLColor.FromHtml("#FFEBEE");
    private static readonly XLColor NoteOptionalFill = XLColor.FromHtml("#E8F5E9");
    private static readonly XLColor ExampleFill = XLColor.FromHtml("#F5F5F5");
    private static readonly XLColor LookupHeaderFill = XLColor.FromHtml("#455A64");

    public byte[] CreateWorkbook(
        IEnumerable<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>>? Rows)> sheets)
    {
        var templateSheets = sheets.Select(s => new ExcelTemplateSheet
        {
            Name = s.SheetName,
            Columns = s.Headers.Select(h => new ExcelColumnSpec { Header = h, Required = false }).ToList(),
            DataRows = s.Rows,
            AddExampleRow = false,
            FreezeHeader = true
        }).ToList();

        return CreateImportTemplate(templateSheets);
    }

    public byte[] CreateImportTemplate(IReadOnlyList<ExcelTemplateSheet> sheets)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheet in sheets)
        {
            var ws = workbook.Worksheets.Add(sheet.Name);
            ApplyTabColor(ws, sheet.TabColorHex);

            if (sheet.Columns is { Count: > 0 })
            {
                // Data sheet; optional Notes render above the grid (e.g. Lookups guide).
                BuildDataSheet(ws, sheet);
            }
            else if (sheet.Notes is { Count: > 0 })
            {
                BuildNotesSheet(ws, sheet);
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public IReadOnlyList<ExcelDataRow> ReadSheet(Stream stream, string sheetName, bool requireSheet = true)
    {
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var ws))
        {
            if (requireSheet)
            {
                throw new InvalidOperationException($"Sheet '{sheetName}' was not found in the Excel file.");
            }

            return [];
        }

        int headerRow = FindHeaderRow(ws);
        var used = ws.RangeUsed();
        if (used is null || headerRow < 1)
        {
            return [];
        }

        int lastRow = used.LastRow().RowNumber();
        int lastCol = used.LastColumn().ColumnNumber();

        var headers = new Dictionary<int, string>();
        for (int c = 1; c <= lastCol; c++)
        {
            string header = NormalizeHeader(ws.Cell(headerRow, c).GetString());
            if (!string.IsNullOrWhiteSpace(header)
                && !string.Equals(header, "Status", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(header, "ErrorMessages", StringComparison.OrdinalIgnoreCase))
            {
                headers[c] = header;
            }
        }

        var rows = new List<ExcelDataRow>();
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            // Skip example / legend helper rows marked in first column notes
            string first = GetCellString(ws.Cell(r, 1));
            if (first.StartsWith("EXAMPLE:", StringComparison.OrdinalIgnoreCase)
                || first.StartsWith("LEGEND:", StringComparison.OrdinalIgnoreCase)
                || first.StartsWith("(example)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool anyValue = false;
            foreach (var (col, header) in headers)
            {
                string cell = GetCellString(ws.Cell(r, col));
                if (!string.IsNullOrWhiteSpace(cell))
                {
                    anyValue = true;
                }

                values[header] = cell;
            }

            if (!anyValue)
            {
                continue;
            }

            rows.Add(new ExcelDataRow { RowNumber = r, Values = values });
        }

        return rows;
    }

    public bool SheetExists(Stream stream, string sheetName)
    {
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        return workbook.Worksheets.TryGetWorksheet(sheetName, out _);
    }

    public byte[] AppendStatusColumns(
        Stream originalStream,
        string sheetName,
        IReadOnlyList<(int RowNumber, string Status, string ErrorMessages)> rowResults)
    {
        return AppendStatusColumns(
            originalStream,
            new Dictionary<string, IReadOnlyList<(int RowNumber, string Status, string ErrorMessages)>>(
                StringComparer.OrdinalIgnoreCase)
            {
                [sheetName] = rowResults
            });
    }

    public byte[] AppendStatusColumns(
        Stream originalStream,
        IReadOnlyDictionary<string, IReadOnlyList<(int RowNumber, string Status, string ErrorMessages)>> resultsBySheet)
    {
        originalStream.Position = 0;
        using var workbook = new XLWorkbook(originalStream);

        foreach (var (sheetName, rowResults) in resultsBySheet)
        {
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var ws))
            {
                continue;
            }

            int headerRow = FindHeaderRow(ws);
            var used = ws.RangeUsed();
            if (used is null)
            {
                continue;
            }

            int statusCol;
            int errorCol;
            string firstHeader = NormalizeHeader(ws.Cell(headerRow, 1).GetString());
            string secondHeader = NormalizeHeader(ws.Cell(headerRow, 2).GetString());

            if (string.Equals(firstHeader, "Status", StringComparison.OrdinalIgnoreCase)
                && string.Equals(secondHeader, "ErrorMessages", StringComparison.OrdinalIgnoreCase))
            {
                statusCol = 1;
                errorCol = 2;
            }
            else
            {
                // Place Status + ErrorMessages at the start (left) of the sheet.
                ws.Column(1).InsertColumnsBefore(2);
                statusCol = 1;
                errorCol = 2;
            }

            StyleLeadingStatusHeader(ws.Cell(headerRow, statusCol), "Status");
            StyleLeadingStatusHeader(ws.Cell(headerRow, errorCol), "ErrorMessages");
            ws.Column(statusCol).Width = 12;
            ws.Column(errorCol).Width = 48;

            var byRow = rowResults.ToDictionary(x => x.RowNumber);
            int maxRow = used.LastRow().RowNumber();
            for (int r = headerRow + 1; r <= maxRow; r++)
            {
                if (!byRow.TryGetValue(r, out var result))
                {
                    continue;
                }

                var statusCell = ws.Cell(r, statusCol);
                var errorCell = ws.Cell(r, errorCol);
                statusCell.Value = result.Status;
                errorCell.Value = result.ErrorMessages ?? string.Empty;
                statusCell.Style.Font.Bold = true;
                errorCell.Style.Alignment.WrapText = true;

                if (string.Equals(result.Status, "Valid", StringComparison.OrdinalIgnoreCase))
                {
                    statusCell.Style.Font.FontColor = OptionalFont;
                    errorCell.Style.Font.FontColor = XLColor.FromHtml("#616161");
                }
                else
                {
                    statusCell.Style.Font.FontColor = RequiredFont;
                    errorCell.Style.Font.FontColor = RequiredFont;
                }
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void StyleLeadingStatusHeader(IXLCell cell, string title)
    {
        cell.Value = title;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 12;
        cell.Style.Font.FontColor = RequiredFont;
        cell.Style.Fill.BackgroundColor = RequiredFill;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#90A4AE");
    }

    private static void BuildDataSheet(IXLWorksheet ws, ExcelTemplateSheet sheet)
    {
        int colCount = sheet.Columns!.Count;
        int row = 1;
        int span = Math.Max(colCount, 3);

        if (!string.IsNullOrWhiteSpace(sheet.BannerTitle))
        {
            ws.Range(row, 1, row, span).Merge();
            var banner = ws.Cell(row, 1);
            banner.Value = sheet.BannerTitle;
            banner.Style.Font.Bold = true;
            banner.Style.Font.FontSize = 14;
            banner.Style.Font.FontColor = XLColor.White;
            banner.Style.Fill.BackgroundColor = BannerFill;
            banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(row).Height = 24;
            row++;

            if (!string.IsNullOrWhiteSpace(sheet.BannerSubtitle))
            {
                ws.Range(row, 1, row, span).Merge();
                var sub = ws.Cell(row, 1);
                sub.Value = sheet.BannerSubtitle;
                sub.Style.Font.FontSize = 10;
                sub.Style.Font.FontColor = XLColor.FromHtml("#37474F");
                sub.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
                ws.Row(row).Height = 18;
                row++;
            }
        }

        if (sheet.Notes is { Count: > 0 })
        {
            foreach (var note in sheet.Notes)
            {
                ws.Range(row, 1, row, span).Merge();
                var cell = ws.Cell(row, 1);
                cell.Value = note.Text;
                cell.Style.Font.FontSize = 10;
                cell.Style.Alignment.WrapText = true;
                ApplyNoteStyle(cell, note.Kind);
                ws.Row(row).Height = 28;
                row++;
            }
        }

        if (sheet.ShowLegend)
        {
            ws.Range(row, 1, row, span).Merge();
            var legend = ws.Cell(row, 1);
            legend.Value = "LEGEND:  Red header = Required *   |   Green header = Optional   |   Grey row = Example (skipped on import)";
            legend.Style.Font.Bold = true;
            legend.Style.Font.FontSize = 10;
            legend.Style.Fill.BackgroundColor = LegendFill;
            legend.Style.Font.FontColor = XLColor.FromHtml("#5D4037");
            ws.Row(row).Height = 18;
            row++;
        }

        int headerRow = row;
        for (int c = 0; c < colCount; c++)
        {
            var colSpec = sheet.Columns[c];
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = colSpec.Required ? $"{colSpec.Header} *" : colSpec.Header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 12;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#90A4AE");

            if (colSpec.Required)
            {
                cell.Style.Fill.BackgroundColor = RequiredFill;
                cell.Style.Font.FontColor = RequiredFont;
            }
            else
            {
                cell.Style.Fill.BackgroundColor = OptionalFill;
                cell.Style.Font.FontColor = OptionalFont;
            }

            ws.Column(c + 1).Width = colSpec.Width;
        }

        ws.Row(headerRow).Height = 22;
        ws.SheetView.FreezeRows(headerRow);

        int dataStart = headerRow + 1;
        if (sheet.AddExampleRow)
        {
            for (int c = 0; c < colCount; c++)
            {
                var colSpec = sheet.Columns[c];
                var cell = ws.Cell(dataStart, c + 1);
                string example = colSpec.Example ?? string.Empty;
                if (c == 0 && !string.IsNullOrWhiteSpace(example))
                {
                    cell.Value = $"(example) {example}";
                }
                else
                {
                    cell.Value = example;
                }

                cell.Style.Font.Italic = true;
                cell.Style.Font.FontColor = XLColor.FromHtml("#757575");
                cell.Style.Fill.BackgroundColor = ExampleFill;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
            }

            dataStart++;
        }

        if (sheet.DataRows is { Count: > 0 })
        {
            for (int r = 0; r < sheet.DataRows.Count; r++)
            {
                var dataRow = sheet.DataRows[r];
                for (int c = 0; c < Math.Min(colCount, dataRow.Count); c++)
                {
                    var cell = ws.Cell(dataStart + r, c + 1);
                    cell.Value = dataRow[c] ?? string.Empty;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFD8DC");
                }
            }
        }
        else
        {
            // Empty typed rows with light grid for user input
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    var cell = ws.Cell(dataStart + r, c + 1);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#ECEFF1");
                }
            }
        }

        // Auto-filter on header
        ws.Range(headerRow, 1, headerRow, colCount).SetAutoFilter();
    }

    private static void ApplyNoteStyle(IXLCell cell, string kind)
    {
        switch (kind.ToLowerInvariant())
        {
            case "required":
                cell.Style.Fill.BackgroundColor = NoteRequiredFill;
                cell.Style.Font.FontColor = RequiredFont;
                cell.Style.Font.Bold = true;
                break;
            case "optional":
                cell.Style.Fill.BackgroundColor = NoteOptionalFill;
                cell.Style.Font.FontColor = OptionalFont;
                break;
            case "warn":
                cell.Style.Fill.BackgroundColor = NoteWarnFill;
                cell.Style.Font.FontColor = XLColor.FromHtml("#E65100");
                cell.Style.Font.Bold = true;
                break;
            case "tip":
                cell.Style.Fill.BackgroundColor = NoteTipFill;
                cell.Style.Font.FontColor = XLColor.FromHtml("#2E7D32");
                break;
            default:
                cell.Style.Fill.BackgroundColor = NoteInfoFill;
                cell.Style.Font.FontColor = XLColor.FromHtml("#1565C0");
                break;
        }

        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#B0BEC5");
    }

    private static void BuildNotesSheet(IXLWorksheet ws, ExcelTemplateSheet sheet)
    {
        ws.Column(1).Width = 100;

        int row = 1;
        if (!string.IsNullOrWhiteSpace(sheet.BannerTitle))
        {
            ws.Cell(row, 1).Value = sheet.BannerTitle;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 16;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = BannerFill;
            ws.Row(row).Height = 28;
            row++;
        }

        if (!string.IsNullOrWhiteSpace(sheet.BannerSubtitle))
        {
            ws.Cell(row, 1).Value = sheet.BannerSubtitle;
            ws.Cell(row, 1).Style.Font.FontSize = 11;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            ws.Row(row).Height = 20;
            row++;
        }

        row++; // spacer

        foreach (var note in sheet.Notes!)
        {
            var cell = ws.Cell(row, 1);
            cell.Value = note.Text;
            cell.Style.Font.FontSize = 11;
            cell.Style.Alignment.WrapText = true;
            ws.Row(row).Height = 32;

            ApplyNoteStyle(cell, note.Kind);
            row++;
        }
    }

    private static void ApplyTabColor(IXLWorksheet ws, string hex)
    {
        try
        {
            string clean = hex.Trim().TrimStart('#');
            ws.TabColor = XLColor.FromHtml("#" + clean);
        }
        catch
        {
            ws.TabColor = XLColor.FromHtml("#546E7A");
        }
    }

    /// <summary>
    /// Finds the header row by looking for a known first header (AdmissionNo / Type) or first bold row.
    /// Falls back to row 1 for plain sheets.
    /// </summary>
    private static int FindHeaderRow(IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used is null)
        {
            return 1;
        }

        int lastRow = Math.Min(used.LastRow().RowNumber(), 15);
        int lastCol = Math.Min(used.LastColumn().ColumnNumber(), 40);
        for (int r = 1; r <= lastRow; r++)
        {
            for (int c = 1; c <= lastCol; c++)
            {
                string header = NormalizeHeader(ws.Cell(r, c).GetString());
                if (string.Equals(header, "AdmissionNo", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(header, "Type", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(header, "FeeMasterName", StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
        }

        return 1;
    }

    private static string NormalizeHeader(string raw)
    {
        string value = (raw ?? string.Empty).Trim();
        if (value.EndsWith("*", StringComparison.Ordinal))
        {
            value = value[..^1].Trim();
        }

        return value;
    }

    private static string GetCellString(IXLCell cell)
    {
        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString("yyyy-MM-dd");
        }

        if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double number))
        {
            if (Math.Abs(number % 1) < 0.0000001)
            {
                return ((long)number).ToString();
            }

            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return cell.GetString().Trim();
    }
}
