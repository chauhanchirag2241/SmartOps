namespace SmartOps.Application.Modules.BulkImport;

public sealed class ImportRowResultDto
{
    public int RowNumber { get; set; }
    public string? AdmissionNo { get; set; }
    public string? DisplayName { get; set; }
    public string Status { get; set; } = "Invalid";
    public List<string> Errors { get; set; } = [];
}

public sealed class StudentImportValidateResultDto
{
    public string? FileError { get; set; }
    public Guid? AcademicYearId { get; set; }
    public string? AcademicYearName { get; set; }
    public int TotalStudents { get; set; }
    public int ValidStudents { get; set; }
    public int InvalidStudents { get; set; }
    public int TotalFeeAssignments { get; set; }
    public int ValidFeeAssignments { get; set; }
    public int InvalidFeeAssignments { get; set; }
    public List<ImportRowResultDto> Students { get; set; } = [];
    public List<ImportRowResultDto> FeeAssignments { get; set; } = [];
    public string? ErrorFileBase64 { get; set; }
    public string ErrorFileName { get; set; } = "student-import-errors.xlsx";
}

public sealed class StudentImportCommitFailureDto
{
    public int? RowNumber { get; set; }
    public string? AdmissionNo { get; set; }
    public string? DisplayName { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class StudentImportCreatedDto
{
    public int? RowNumber { get; set; }
    public string? AdmissionNo { get; set; }
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    /// <summary>Active or Inactive.</summary>
    public string Status { get; set; } = "Active";
}

public sealed class StudentImportCommitResultDto
{
    public string? FileError { get; set; }
    public int CreatedStudents { get; set; }
    public int FeeAssignmentsApplied { get; set; }
    public int SkippedInvalidStudents { get; set; }
    public int SkippedInvalidFeeAssignments { get; set; }
    public List<StudentImportCommitFailureDto> Failures { get; set; } = [];
    public List<StudentImportCreatedDto> Created { get; set; } = [];
    public StudentImportValidateResultDto? Validation { get; set; }
}
