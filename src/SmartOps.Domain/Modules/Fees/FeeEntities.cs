using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Fees;

public class FeeStructureEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public int VersionNumber { get; set; }
    public FeeStructureVersionStatus Status { get; set; } = FeeStructureVersionStatus.Draft;
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? PublishedOn { get; set; }
    public DateTime? ActivatedOn { get; set; }
}

public class FeeHeadEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeStructureId { get; set; }
    public string Name { get; set; } = null!;
    public FeeCategory Category { get; set; }
    /// <summary>Stored in DB column <c>frequency</c> for schema compatibility.</summary>
    public FeeCollectionType CollectionType { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsRefundable { get; set; }
    public bool StudentWiseDifferentAmount { get; set; }
}

public class ClassFeeAmountEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeStructureId { get; set; }
    public Guid ClassGroupId { get; set; }
    public Guid FeeHeadId { get; set; }
    public Guid AcademicYearId { get; set; }
    /// <summary>One-time fee amount, or annual total for period-wise fees.</summary>
    public decimal Amount { get; set; }
}

public class ClassFeePeriodAmountEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassFeeAmountId { get; set; }
    public int PeriodIndex { get; set; }
    public decimal Amount { get; set; }
}

public class FeePaymentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeStructureId { get; set; }
    public decimal Amount { get; set; }
    public FeePaymentMode PaymentMode { get; set; }
    public string? TransactionNo { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Remarks { get; set; }
    public string? ReceiptNo { get; set; }
}

public class FeePaymentAllocationEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid FeeHeadId { get; set; }
    public Guid? InstallmentId { get; set; }
    public decimal Amount { get; set; }
}

public class ClassFeeInstallmentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeStructureId { get; set; }
    public Guid ClassGroupId { get; set; }
    public Guid FeeHeadId { get; set; }
    public Guid AcademicYearId { get; set; }
    public int PeriodIndex { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Amount { get; set; }
}
