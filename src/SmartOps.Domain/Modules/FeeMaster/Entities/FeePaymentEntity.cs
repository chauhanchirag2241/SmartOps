using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

[TrackHistory]
public sealed class FeePaymentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeMasterId { get; set; }
    public Guid? AcademicPeriodId { get; set; }
    public DateTime PaymentDate { get; set; }
    /// <summary>Cash, UPI, Cheque, Card, BankTransfer, Other.</summary>
    public string PaymentMethod { get; set; } = "Cash";
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }
    public Guid? CollectedByUserId { get; set; }
}

[TrackHistory]
public sealed class FeePaymentLineEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid FeePaymentId { get; set; }
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public decimal DueAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
}
