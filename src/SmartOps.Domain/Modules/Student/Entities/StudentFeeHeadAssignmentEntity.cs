using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentFeeHeadAssignmentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeStructureVersionId { get; set; }
    public Guid FeeTypeId { get; set; }
    public bool IsIncluded { get; set; } = true;
    /// <summary>Per-student annual override for this fee head; null uses class default.</summary>
    public decimal? CustomAnnualAmount { get; set; }
}
