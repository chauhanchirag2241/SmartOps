using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentFeeHeadAssignmentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeStructureId { get; set; }
    public Guid FeeHeadId { get; set; }
    public bool IsIncluded { get; set; } = true;
    /// <summary>Per-student annual override for this fee head; null uses class default.</summary>
    public decimal? CustomAnnualAmount { get; set; }
}
