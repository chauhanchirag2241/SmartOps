namespace SmartOps.Application.Modules.BulkImport;

public sealed class EmployeeImportValidateResultDto
{
    public string? FileError { get; set; }
    public int TotalEmployees { get; set; }
    public int ValidEmployees { get; set; }
    public int InvalidEmployees { get; set; }
    public List<ImportRowResultDto> Employees { get; set; } = [];
    public string? ErrorFileBase64 { get; set; }
    public string ErrorFileName { get; set; } = "employee-import-errors.xlsx";
}

public sealed class EmployeeImportCommitFailureDto
{
    public int? RowNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? DisplayName { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class EmployeeImportCreatedDto
{
    public int? RowNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    /// <summary>Active or Inactive.</summary>
    public string Status { get; set; } = "Active";
}

public sealed class EmployeeImportCommitResultDto
{
    public string? FileError { get; set; }
    public int CreatedEmployees { get; set; }
    public int SkippedInvalidEmployees { get; set; }
    public List<EmployeeImportCommitFailureDto> Failures { get; set; } = [];
    public List<EmployeeImportCreatedDto> Created { get; set; } = [];
    public EmployeeImportValidateResultDto? Validation { get; set; }
}
