namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeInstallmentService
{
    Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task RegenerateForVersionAsync(
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default);
}
