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
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (await _installmentRepo.VersionHasInstallmentPaymentsAsync(feeStructureVersionId, ct).ConfigureAwait(false))
        {
            return;
        }

        await _installmentRepo.RegenerateForClassVersionAsync(classId, feeStructureVersionId, academicYearId, ct)
            .ConfigureAwait(false);
    }

    public async Task RegenerateForVersionAsync(
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (await _installmentRepo.VersionHasInstallmentPaymentsAsync(feeStructureVersionId, ct).ConfigureAwait(false))
        {
            return;
        }

        await _installmentRepo.RegenerateForVersionAsync(feeStructureVersionId, academicYearId, ct)
            .ConfigureAwait(false);
    }
}
