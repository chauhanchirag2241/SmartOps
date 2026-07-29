namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeInstallmentService
{
    Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task RegenerateForVersionAsync(
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default);
}
