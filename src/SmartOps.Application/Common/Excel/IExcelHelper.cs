namespace SmartOps.Application.Common.Excel;

/// <summary>One data row read from an Excel sheet (1-based Excel row number).</summary>
public sealed class ExcelDataRow
{
    public int RowNumber { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ExcelColumnSpec
{
    public required string Header { get; init; }
    public bool Required { get; init; }
    public string? Example { get; init; }
    public double Width { get; init; } = 16;
}

public sealed class ExcelNoteLine
{
    public required string Text { get; init; }

    /// <summary>info | tip | warn | required | optional</summary>
    public string Kind { get; init; } = "info";
}

public sealed class ExcelTemplateSheet
{
    public required string Name { get; init; }

    /// <summary>Hex without #, e.g. C62828</summary>
    public string TabColorHex { get; init; } = "546E7A";

    public IReadOnlyList<ExcelColumnSpec>? Columns { get; init; }

    /// <summary>Data rows under header (column order matches Columns).</summary>
    public IReadOnlyList<IReadOnlyList<string>>? DataRows { get; init; }

    /// <summary>When set, sheet is built as a notes/instructions layout (no data grid).</summary>
    public IReadOnlyList<ExcelNoteLine>? Notes { get; init; }

    public string? BannerTitle { get; init; }
    public string? BannerSubtitle { get; init; }
    public bool AddExampleRow { get; init; } = true;
    public bool ShowLegend { get; init; } = true;
    public bool FreezeHeader { get; init; } = true;
}

public interface IExcelHelper
{
    byte[] CreateWorkbook(
        IEnumerable<(string SheetName, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>>? Rows)> sheets);

    /// <summary>User-friendly import template with colored headers, tabs, notes, and example rows.</summary>
    byte[] CreateImportTemplate(IReadOnlyList<ExcelTemplateSheet> sheets);

    IReadOnlyList<ExcelDataRow> ReadSheet(Stream stream, string sheetName, bool requireSheet = true);

    bool SheetExists(Stream stream, string sheetName);

    byte[] AppendStatusColumns(
        Stream originalStream,
        string sheetName,
        IReadOnlyList<(int RowNumber, string Status, string ErrorMessages)> rowResults);

    byte[] AppendStatusColumns(
        Stream originalStream,
        IReadOnlyDictionary<string, IReadOnlyList<(int RowNumber, string Status, string ErrorMessages)>> resultsBySheet);
}
