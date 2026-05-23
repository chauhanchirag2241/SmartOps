using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class ClassFeeAmountService : IClassFeeAmountService
{
    private readonly IClassFeeAmountRepository _repo;
    private readonly IFeeStructureRepository _feeStructureRepo;

    public ClassFeeAmountService(IClassFeeAmountRepository repo, IFeeStructureRepository feeStructureRepo)
    {
        _repo = repo;
        _feeStructureRepo = feeStructureRepo;
    }

    public async Task<Result<IList<ClassFeeSummaryDto>>> GetClassSummariesAsync(
        Guid academicYearId,
        Guid? feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (academicYearId == Guid.Empty)
        {
            return Result<IList<ClassFeeSummaryDto>>.Failure("Academic year is required.");
        }

        Guid versionId = await ResolveVersionIdAsync(academicYearId, feeStructureVersionId, ct).ConfigureAwait(false);
        if (versionId == Guid.Empty)
        {
            return Result<IList<ClassFeeSummaryDto>>.Failure(
                "No active fee structure for this academic year. Publish and activate the fee structure first.");
        }

        IList<ClassFeeSummaryRow> rows = await _repo.GetClassSummariesAsync(academicYearId, versionId, ct).ConfigureAwait(false);
        IList<ClassFeeSummaryDto> dtos = rows
            .Select(r => new ClassFeeSummaryDto(r.ClassId, r.ClassName, r.StudentCount, r.TotalAmount))
            .ToList();
        return Result<IList<ClassFeeSummaryDto>>.Success(dtos);
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> GetClassAmountsAsync(
        Guid classId,
        Guid academicYearId,
        Guid? feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class and academic year are required.");
        }

        Guid versionId = await ResolveVersionIdAsync(academicYearId, feeStructureVersionId, ct).ConfigureAwait(false);
        if (versionId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "No active fee structure for this academic year. Publish and activate the fee structure first.");
        }

        FeeStructureVersionEntity? version = await _feeStructureRepo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Fee structure version not found.");
        }

        IList<ClassFeeSummaryRow> summaries = await _repo.GetClassSummariesAsync(academicYearId, versionId, ct).ConfigureAwait(false);
        ClassFeeSummaryRow? summary = summaries.FirstOrDefault(s => s.ClassId == classId);
        IList<ClassFeeAmountRow> rows = await _repo.GetAmountsByClassAsync(classId, versionId, ct).ConfigureAwait(false);

        IList<ClassFeeAmountItemDto> items = rows
            .Select(r => new ClassFeeAmountItemDto(
                r.FeeTypeId,
                r.FeeTypeName,
                FeeLabelHelper.CategoryLabel((FeeCategory)r.Category),
                FeeLabelHelper.FrequencyLabel((FeeFrequency)r.Frequency),
                r.Amount))
            .ToList();

        return Result<ClassFeeAmountsResponseDto>.Success(new ClassFeeAmountsResponseDto(
            classId,
            summary?.ClassName ?? "Class",
            academicYearId,
            versionId,
            version.VersionNumber,
            FeeLabelHelper.VersionStatusLabel(version.Status),
            version.Status == FeeStructureVersionStatus.Draft,
            items.Sum(i => i.Amount),
            items));
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> SaveClassAmountsAsync(
        Guid classId,
        SaveClassFeeAmountsRequestDto request,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || request.AcademicYearId == Guid.Empty || request.FeeStructureVersionId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class, academic year and fee structure version are required.");
        }

        FeeStructureVersionEntity? version = await _feeStructureRepo.GetVersionByIdAsync(request.FeeStructureVersionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Fee structure version not found.");
        }

        if (version.Status != FeeStructureVersionStatus.Draft)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Only draft fee structures can be edited.");
        }

        IList<(Guid FeeTypeId, decimal Amount)> amounts = request.Amounts
            .Select(a => (a.FeeTypeId, a.Amount))
            .ToList();

        await _repo.UpsertAmountsAsync(classId, request.AcademicYearId, request.FeeStructureVersionId, amounts, ct).ConfigureAwait(false);
        return await GetClassAmountsAsync(classId, request.AcademicYearId, request.FeeStructureVersionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// When no version is specified (e.g. student admission preview), only the active structure applies.
    /// Draft (and published-but-not-active) versions are never used implicitly.
    /// </summary>
    private async Task<Guid> ResolveVersionIdAsync(Guid academicYearId, Guid? feeStructureVersionId, CancellationToken ct)
    {
        if (feeStructureVersionId.HasValue && feeStructureVersionId.Value != Guid.Empty)
        {
            return feeStructureVersionId.Value;
        }

        FeeStructureVersionEntity? active = await _feeStructureRepo.GetActiveVersionForYearAsync(academicYearId, ct).ConfigureAwait(false);
        return active?.Id ?? Guid.Empty;
    }
}
