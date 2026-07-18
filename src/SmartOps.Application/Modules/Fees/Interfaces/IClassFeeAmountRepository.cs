namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeAmountRepository
{
    Task<IList<ClassFeeSummaryRow>> GetClassSummariesAsync(Guid academicYearId, Guid feeStructureVersionId, CancellationToken ct = default);
    Task<IList<ClassFeeAmountRow>> GetAmountsByClassAsync(Guid classId, Guid feeStructureVersionId, CancellationToken ct = default);
    Task UpsertAmountsAsync(
        Guid classId,
        Guid academicYearId,
        Guid feeStructureVersionId,
        IList<ClassFeeAmountUpsertRow> amounts,
        CancellationToken ct = default);

    /// <summary>True when this class has fee amounts or installments with amount &gt; 0 (not merely zero placeholder rows).</summary>
    Task<bool> ClassHasConfiguredAmountsAsync(Guid classId, Guid feeStructureVersionId, CancellationToken ct = default);
}

public sealed class ClassFeeSummaryRow
{
    public Guid ClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public int StudentCount { get; init; }
    public decimal TotalAmount { get; init; }
}

public sealed class ClassFeeAmountRow
{
    public Guid FeeTypeId { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public int Category { get; init; }
    public int CollectionType { get; init; }
    public decimal Amount { get; init; }
    public IList<ClassFeePeriodAmountRow> PeriodAmounts { get; set; } = [];
    public bool IsMandatory { get; init; }
    public bool StudentWiseDifferentAmount { get; init; }
}

public sealed class ClassFeeAmountUpsertRow
{
    public Guid FeeTypeId { get; init; }
    public decimal Amount { get; init; }
    public IList<ClassFeePeriodAmountRow> PeriodAmounts { get; init; } = [];
}

public sealed class ClassFeePeriodAmountRow
{
    public Guid FeeTypeId { get; init; }
    public int PeriodIndex { get; init; }
    public decimal Amount { get; init; }
}
