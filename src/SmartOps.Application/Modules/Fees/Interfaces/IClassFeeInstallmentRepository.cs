namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeInstallmentRepository
{
    Task<IList<ClassFeeInstallmentRow>> GetByClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        CancellationToken ct = default);

    Task<IList<ClassFeeAmountForInstallmentRow>> GetClassAmountsForVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<IList<Guid>> GetClassIdsWithAmountsForVersionAsync(
        Guid feeStructureId,
        CancellationToken ct = default);

    Task<bool> VersionHasInstallmentPaymentsAsync(Guid feeStructureId, CancellationToken ct = default);

    Task RegenerateForClassFeeHeadAsync(
        Guid classId,
        Guid feeStructureId,
        Guid feeHeadId,
        Guid academicYearId,
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods,
        CancellationToken ct = default);

    Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task RegenerateForVersionAsync(
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<IList<InstallmentPaidRow>> GetPaidByInstallmentAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default);

    Task<bool> InstallmentBelongsToClassVersionAsync(
        Guid installmentId,
        Guid classId,
        Guid feeStructureId,
        CancellationToken ct = default);

    Task<bool> IsInstallmentSchemaReadyAsync(CancellationToken ct = default);

    Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);
}

public sealed class ClassFeeInstallmentRow
{
    public Guid Id { get; init; }
    public Guid FeeHeadId { get; init; }
    public string FeeHeadName { get; init; } = string.Empty;
    public int Category { get; init; }
    public int CollectionType { get; init; }
    public int PeriodIndex { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Amount { get; init; }
}

public sealed class ClassFeeAmountForInstallmentRow
{
    public Guid FeeHeadId { get; init; }
    public string FeeHeadName { get; init; } = string.Empty;
    public int Category { get; init; }
    public int CollectionType { get; init; }
    public decimal Amount { get; init; }
    public IList<ClassFeePeriodAmountRow> PeriodAmounts { get; set; } = [];
}

public sealed class InstallmentPaidRow
{
    public Guid InstallmentId { get; init; }
    public Guid FeeHeadId { get; init; }
    public decimal PaidAmount { get; init; }
}
