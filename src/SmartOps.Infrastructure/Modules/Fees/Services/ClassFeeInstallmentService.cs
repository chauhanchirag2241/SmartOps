using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class ClassFeeInstallmentService : IClassFeeInstallmentService
{
    private readonly IClassFeeInstallmentRepository _installmentRepo;

    public ClassFeeInstallmentService(IClassFeeInstallmentRepository installmentRepo) =>
        _installmentRepo = installmentRepo;

    public async Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await _installmentRepo.IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await _installmentRepo.VersionHasInstallmentPaymentsAsync(feeStructureId, ct).ConfigureAwait(false))
        {
            return;
        }

        await _installmentRepo.RegenerateForClassVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);
    }

    public async Task RegenerateForVersionAsync(
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await _installmentRepo.IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await _installmentRepo.VersionHasInstallmentPaymentsAsync(feeStructureId, ct).ConfigureAwait(false))
        {
            return;
        }

        await _installmentRepo.RegenerateForVersionAsync(feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);
    }

    public Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default) =>
        _installmentRepo.EnsureMissingInstallmentsForClassVersionAsync(
            classId,
            feeStructureId,
            academicYearId,
            ct);
}
