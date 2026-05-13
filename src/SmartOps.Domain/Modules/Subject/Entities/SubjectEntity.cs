using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Subject.Enums;

namespace SmartOps.Domain.Modules.Subject.Entities;

public sealed class SubjectEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public SubjectType SubjectType { get; set; }
    public SubjectCategory SubjectCategory { get; set; }
    public int Medium { get; set; } // Mapping to Medium enum
    public string AssignedClasses { get; set; } = "[]"; // Store as JSON string
    public int PeriodsPerWeek { get; set; }
    public string PeriodDuration { get; set; } = string.Empty;
    public string TeachingDays { get; set; } = "[]"; // Store as JSON string
    public int MaxTheory { get; set; }
    public int MaxPractical { get; set; }
    public int PassingMarks { get; set; }
    public GradeSystem GradeSystem { get; set; }
    public string? SyllabusTextbook { get; set; }
    public Curriculum Curriculum { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
