namespace SmartOps.Domain.Modules.FeeMaster;

public sealed class FeeCollectionStudentInfo
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string? Mobile { get; set; }
    public string? ClassName { get; set; }
    public string? Section { get; set; }
    public string? RollNumber { get; set; }
    public string? AdmissionNo { get; set; }
    public string Initials { get; set; } = string.Empty;
}

public sealed class FeeCollectionHeadModel
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
    public decimal DueAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance => Math.Max(0, DueAmount - PaidAmount);
    public bool IsExcluded { get; set; }
}

public sealed class FeeCollectionMasterCardModel
{
    public Guid FeeMasterId { get; set; }
    public string FeeName { get; set; } = string.Empty;
    public string FeeType { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? DefaultDueDate { get; set; }
    public Guid? AcademicPeriodId { get; set; }
    public string? PeriodLabel { get; set; }
    public decimal TotalDue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public string Status { get; set; } = "Pending";
    /// <summary>True when PublishedOn is null or today is on/after PublishedOn.</summary>
    public bool IsPublished { get; set; }
    public bool CanCollect { get; set; }
    public bool StudentAmountsLocked { get; set; }
    public IReadOnlyList<FeeCollectionHeadModel> Heads { get; set; } = [];
}

public sealed class FeeCollectionHistoryLineModel
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public decimal DueAmount { get; set; }
    public decimal PaidAmount { get; set; }
    /// <summary>Remaining on this fee head after this payment (chronological).</summary>
    public decimal BalanceAfter { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
}

public sealed class FeeCollectionStudentSummaryModel
{
    public Guid StudentId { get; set; }
    public decimal TotalDue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public string Status { get; set; } = "Pending";
}

public sealed class FeeCollectionHistoryPaymentModel
{
    public Guid PaymentId { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public Guid? AcademicPeriodId { get; set; }
    public string? PeriodLabel { get; set; }
    public string? CollectedBy { get; set; }
    public string? Remarks { get; set; }
    public IReadOnlyList<FeeCollectionHistoryLineModel> Lines { get; set; } = [];
}

/// <summary>One fee-head due row within a period for collection.</summary>
public sealed class FeeCollectionPeriodHeadDue
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsEditable { get; set; }
    public Guid AcademicPeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public int PeriodIndex { get; set; }
    public decimal DueAmount { get; set; }
    public bool IsExcluded { get; set; }
}

public sealed class FeeCollectionHistoryRowModel
{
    public Guid FeeMasterId { get; set; }
    public string FeeName { get; set; } = string.Empty;
    public decimal TotalDue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalPending { get; set; }
    public string Status { get; set; } = "Pending";
    public IReadOnlyList<FeeCollectionHistoryPaymentModel> Payments { get; set; } = [];
}

public sealed class FeeCollectionDetailModel
{
    public FeeCollectionStudentInfo Student { get; set; } = new();
    public decimal SummaryTotal { get; set; }
    public decimal SummaryPaid { get; set; }
    public decimal SummaryPending { get; set; }
    public IReadOnlyList<FeeCollectionMasterCardModel> DueCards { get; set; } = [];
    public IReadOnlyList<FeeCollectionHistoryRowModel> History { get; set; } = [];
}
