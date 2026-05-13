using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentAcademicEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly? AdmissionDate { get; set; }
    public string AcademicYear { get; set; } = null!;
    public Guid ClassId { get; set; }
    public string? RollNumber { get; set; }
}
