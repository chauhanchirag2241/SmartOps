namespace SmartOps.Domain.Modules.AcademicYear;

/// <summary>
/// Legacy DB column values. Display status (Current / Upcoming / Past) is derived from dates;
/// soft-deleted rows use IsActive=false (list status "Deleted").
/// </summary>
public enum AcademicYearStatus
{
    Draft = 1,
    Current = 2,
    Archived = 3,
}
