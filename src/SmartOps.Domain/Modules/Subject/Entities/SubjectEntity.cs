using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Modules.Subject;

namespace SmartOps.Domain.Modules.Subject.Entities;

[TrackHistory]
public sealed class SubjectEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid? ClassGroupId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public SubjectType? SubjectType { get; set; }
    public SubjectCategory? SubjectCategory { get; set; }
    public int? Medium { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
