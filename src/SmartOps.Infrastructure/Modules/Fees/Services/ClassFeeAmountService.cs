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
                "No published fee structure for this academic year. Publish the fee structure first.");
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
                "No published fee structure for this academic year. Publish the fee structure first.");
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

        bool classHasSavedAmounts = await _repo
            .ClassHasSavedAmountsAsync(classId, versionId, ct)
            .ConfigureAwait(false);

        return Result<ClassFeeAmountsResponseDto>.Success(new ClassFeeAmountsResponseDto(
            classId,
            summary?.ClassName ?? "Class",
            academicYearId,
            versionId,
            version.VersionNumber,
            FeeLabelHelper.VersionStatusLabel(version.Status),
            IsClassAmountsEditable(version.Status, classHasSavedAmounts),
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

        bool classHasSavedAmounts = await _repo
            .ClassHasSavedAmountsAsync(classId, request.FeeStructureVersionId, ct)
            .ConfigureAwait(false);
        if (!IsClassAmountsEditable(version.Status, classHasSavedAmounts))
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                version.Status == FeeStructureVersionStatus.Active
                    ? "This class already has fee amounts on the active structure. Create a new draft version from Fee Structure to change them."
                    : "This fee structure version cannot be edited.");
        }

        IList<(Guid FeeTypeId, decimal Amount)> amounts = request.Amounts
            .Select(a => (a.FeeTypeId, a.Amount))
            .ToList();

        await _repo.UpsertAmountsAsync(classId, request.AcademicYearId, request.FeeStructureVersionId, amounts, ct).ConfigureAwait(false);
        return await GetClassAmountsAsync(classId, request.AcademicYearId, request.FeeStructureVersionId, ct).ConfigureAwait(false);
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> GetClassAmountsForAdmissionAsync(
        Guid classId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class and academic year are required.");
        }

        FeeStructureVersionEntity? admissionVersion = await _feeStructureRepo
            .GetAdmissionVersionForYearAsync(academicYearId, ct)
            .ConfigureAwait(false);
        if (admissionVersion is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "No published fee structure for this academic year. Publish the fee structure before admitting students.");
        }

        Result<ClassFeeAmountsResponseDto> result = await GetClassAmountsAsync(
            classId,
            academicYearId,
            admissionVersion.Id,
            ct).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            return result;
        }

        if (!IsAdmissionFeeStructureStatus(admissionVersion.Status))
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "No published fee structure for this academic year. Publish the fee structure before admitting students.");
        }

        if (result.Value.Items.Count == 0)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "Published fee structure has no fee heads configured for this class.");
        }

        return result;
    }

    private static bool IsAdmissionFeeStructureStatus(FeeStructureVersionStatus status) =>
        status is FeeStructureVersionStatus.Published or FeeStructureVersionStatus.Active;

    /// <summary>
    /// Draft and Published: full edit. Active: only classes with no saved amounts (e.g. newly added classes).
    /// </summary>
    private static bool IsClassAmountsEditable(FeeStructureVersionStatus status, bool classHasSavedAmounts) =>
        status switch
        {
            FeeStructureVersionStatus.Draft => true,
            FeeStructureVersionStatus.Published => true,
            FeeStructureVersionStatus.Active => !classHasSavedAmounts,
            _ => false
        };

    /// <summary>
    /// When no version is specified (e.g. student admission preview), use active or latest published — never draft.
    /// </summary>
    private async Task<Guid> ResolveVersionIdAsync(Guid academicYearId, Guid? feeStructureVersionId, CancellationToken ct)
    {
        if (feeStructureVersionId.HasValue && feeStructureVersionId.Value != Guid.Empty)
        {
            return feeStructureVersionId.Value;
        }

        FeeStructureVersionEntity? admissionVersion = await _feeStructureRepo
            .GetAdmissionVersionForYearAsync(academicYearId, ct)
            .ConfigureAwait(false);
        return admissionVersion?.Id ?? Guid.Empty;
    }
}
