using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Fees;

public class FeeStructureVersionEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public int VersionNumber { get; set; }
    public FeeStructureVersionStatus Status { get; set; } = FeeStructureVersionStatus.Draft;
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? PublishedOn { get; set; }
    public DateTime? ActivatedOn { get; set; }
}

public class FeeTypeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeStructureVersionId { get; set; }
    public string Name { get; set; } = null!;
    public FeeCategory Category { get; set; }
    public FeeFrequency Frequency { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsRefundable { get; set; }
}

public class FeeSettingsEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public FeePaymentCycle PaymentCycle { get; set; } = FeePaymentCycle.Quarterly;
    public decimal LateFeePerDay { get; set; }
    public Guid? DefaultAcademicYearId { get; set; }
}

public class ClassFeeAmountEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeStructureVersionId { get; set; }
    public Guid ClassId { get; set; }
    public Guid FeeTypeId { get; set; }
    public Guid AcademicYearId { get; set; }
    public decimal Amount { get; set; }
}

public class FeePaymentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid FeeStructureVersionId { get; set; }
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
    public Guid FeeTypeId { get; set; }
    public decimal Amount { get; set; }
}
