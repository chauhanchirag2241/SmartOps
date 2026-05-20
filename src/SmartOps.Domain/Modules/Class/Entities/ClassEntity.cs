using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Class.Entities;

public class ClassEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string ClassName { get; set; } = null!;
    public int Section { get; set; }
    public int StreamGroup { get; set; }
    public Guid AcademicYearId { get; set; }
    public int Capacity { get; set; }
    public string? ClassTeacher { get; set; }
    public string? RoomNumber { get; set; }
    public int Shift { get; set; }
    public int Medium { get; set; }
    public string? Description { get; set; }
}
