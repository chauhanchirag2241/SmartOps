using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentAcademicEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateTime? AdmissionDate { get; set; }
    public string AcademicYear { get; set; } = null!;
    public string Class { get; set; } = null!;
    public string Section { get; set; } = null!;
    public string? RollNumber { get; set; }
}
