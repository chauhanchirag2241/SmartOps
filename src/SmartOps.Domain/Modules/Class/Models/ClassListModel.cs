namespace SmartOps.Domain.Modules.Class.Models;

/// <summary>
/// Flat projection returned by the paged list query.
/// Enum int values are resolved to display strings in the repository.
/// </summary>
public class ClassListModel
{
    public Guid Id { get; set; }
    public string ClassName { get; set; } = null!;
    public string Section { get; set; } = null!;
    public string StreamGroup { get; set; } = null!;
    public string AcademicYear { get; set; } = null!;
    public int Capacity { get; set; }
    public string? ClassTeacher { get; set; }
    public string? RoomNumber { get; set; }
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
