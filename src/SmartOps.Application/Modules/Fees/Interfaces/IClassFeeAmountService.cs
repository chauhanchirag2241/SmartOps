using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IClassFeeAmountService
{
    Task<Result<IList<ClassFeeSummaryDto>>> GetClassSummariesAsync(
        Guid academicYearId,
        Guid? feeStructureVersionId,
        CancellationToken ct = default);

    Task<Result<ClassFeeAmountsResponseDto>> GetClassAmountsAsync(
        Guid classId,
        Guid academicYearId,
        Guid? feeStructureVersionId,
        CancellationToken ct = default);

    /// <summary>Active fee structure only — for student admission preview (never draft/published-only).</summary>
    Task<Result<ClassFeeAmountsResponseDto>> GetClassAmountsForAdmissionAsync(
        Guid classId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<Result<ClassFeeAmountsResponseDto>> SaveClassAmountsAsync(
        Guid classId,
        SaveClassFeeAmountsRequestDto request,
        CancellationToken ct = default);
}
