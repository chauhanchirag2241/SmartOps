using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentFeeInstallmentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeStructureId { get; set; }
    public Guid? ClassFeeInstallmentId { get; set; }
    public Guid FeeHeadId { get; set; }
    public int PeriodIndex { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Amount { get; set; }
}
