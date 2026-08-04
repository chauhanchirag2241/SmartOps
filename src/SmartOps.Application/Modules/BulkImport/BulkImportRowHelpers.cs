using System.Globalization;
using SmartOps.Application.Common.Excel;

namespace SmartOps.Application.Modules.BulkImport;

/// <summary>Shared Excel row helpers for student / employee (and future) bulk imports.</summary>
public static class BulkImportRowHelpers
{
    public static string Get(ExcelDataRow row, string key) =>
        row.Values.TryGetValue(key, out string? v) ? (v ?? string.Empty).Trim() : string.Empty;

    public static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsYes(string value) =>
        string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "True", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Enabled", StringComparison.OrdinalIgnoreCase);

    public static bool IsNo(string value) =>
        string.Equals(value, "N", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "No", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "False", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "Disabled", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseDate(string raw, out DateOnly? date)
    {
        date = null;
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"];
        if (DateOnly.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d))
        {
            date = d;
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime dt)
            || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }
}
