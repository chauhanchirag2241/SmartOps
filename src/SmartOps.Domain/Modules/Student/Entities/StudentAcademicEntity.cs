using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentAcademicEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateOnly? AdmissionDate { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public Guid? FeeStructureVersionId { get; set; }
    public string? RollNumber { get; set; }
}
