using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Teacher.Entities;

public sealed class ClassSubjectTeacherMappingEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid ClassGroupId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid AcademicYearId { get; set; }
}
