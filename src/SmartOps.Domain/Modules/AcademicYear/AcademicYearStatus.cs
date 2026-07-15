namespace SmartOps.Domain.Modules.AcademicYear;

/// <summary>
/// Lifecycle of an academic year. Soft-deleted rows use IsActive=false (list status "Deleted").
/// </summary>
public enum AcademicYearStatus
{
    Draft = 1,
    Current = 2,
    Archived = 3,
}
