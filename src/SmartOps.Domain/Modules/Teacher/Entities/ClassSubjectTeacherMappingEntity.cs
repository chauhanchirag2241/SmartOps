using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Teacher.Entities;

public sealed class ClassSubjectTeacherMappingEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public Guid AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; }
}
