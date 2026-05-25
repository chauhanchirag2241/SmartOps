namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeInstallmentRepository
{
    Task<IList<ClassFeeInstallmentRow>> GetByClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<IList<ClassFeeAmountForInstallmentRow>> GetClassAmountsForVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<IList<Guid>> GetClassIdsWithAmountsForVersionAsync(
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> VersionHasInstallmentPaymentsAsync(Guid feeStructureVersionId, CancellationToken ct = default);

    Task RegenerateForClassFeeTypeAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid feeTypeId,
        Guid academicYearId,
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods,
        CancellationToken ct = default);

    Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task RegenerateForVersionAsync(
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<IList<InstallmentPaidRow>> GetPaidByInstallmentAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> InstallmentBelongsToClassVersionAsync(
        Guid installmentId,
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default);

    Task<bool> IsInstallmentSchemaReadyAsync(CancellationToken ct = default);

    Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);
}

public sealed class ClassFeeInstallmentRow
{
    public Guid Id { get; init; }
    public Guid FeeTypeId { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public int CollectionType { get; init; }
    public int PeriodIndex { get; init; }
    public string PeriodLabel { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal Amount { get; init; }
}

public sealed class ClassFeeAmountForInstallmentRow
{
    public Guid FeeTypeId { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public int CollectionType { get; init; }
    public decimal Amount { get; init; }
    public decimal Semester1Amount { get; init; }
    public decimal Semester2Amount { get; init; }
}

public sealed class InstallmentPaidRow
{
    public Guid InstallmentId { get; init; }
    public Guid FeeTypeId { get; init; }
    public decimal PaidAmount { get; init; }
}
