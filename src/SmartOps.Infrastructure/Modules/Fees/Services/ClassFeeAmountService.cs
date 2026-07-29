using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Domain.Modules.AcademicPeriod;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class ClassFeeAmountService : IClassFeeAmountService
{
    private readonly IClassFeeAmountRepository _repo;
    private readonly IFeeStructureRepository _feeStructureRepo;
    private readonly IClassFeeInstallmentService _installmentService;
    private readonly IClassFeeInstallmentRepository _installmentRepo;
    private readonly IAcademicPeriodRepository _periodRepo;

    public ClassFeeAmountService(
        IClassFeeAmountRepository repo,
        IFeeStructureRepository feeStructureRepo,
        IClassFeeInstallmentService installmentService,
        IClassFeeInstallmentRepository installmentRepo,
        IAcademicPeriodRepository periodRepo)
    {
        _repo = repo;
        _feeStructureRepo = feeStructureRepo;
        _installmentService = installmentService;
        _installmentRepo = installmentRepo;
        _periodRepo = periodRepo;
    }

    public async Task<Result<IList<ClassFeeSummaryDto>>> GetClassSummariesAsync(
        Guid academicYearId,
        Guid? feeStructureId,
        CancellationToken ct = default)
    {
        if (academicYearId == Guid.Empty)
        {
            return Result<IList<ClassFeeSummaryDto>>.Failure("Academic year is required.");
        }

        Guid versionId = await ResolveVersionIdAsync(academicYearId, feeStructureId, ct).ConfigureAwait(false);
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
        Guid? feeStructureId,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class and academic year are required.");
        }

        Guid classGroupId = await _repo.ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class not found.");
        }

        Guid versionId = await ResolveVersionIdAsync(academicYearId, feeStructureId, ct).ConfigureAwait(false);
        if (versionId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "No published fee structure for this academic year. Publish the fee structure first.");
        }

        FeeStructureEntity? version = await _feeStructureRepo.GetVersionByIdAsync(versionId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Fee structure version not found.");
        }

        await _installmentService
            .EnsureMissingInstallmentsForClassVersionAsync(classGroupId, versionId, academicYearId, ct)
            .ConfigureAwait(false);

        IList<ClassFeeSummaryRow> summaries = await _repo.GetClassSummariesAsync(academicYearId, versionId, ct).ConfigureAwait(false);
        ClassFeeSummaryRow? summary = summaries.FirstOrDefault(s => s.ClassId == classGroupId);
        IList<ClassFeeAmountRow> rows = await _repo.GetAmountsByClassAsync(classGroupId, academicYearId, versionId, ct).ConfigureAwait(false);
        IReadOnlyList<ClassAcademicPeriodEntity> periods = await _periodRepo
            .GetByClassAsync(classGroupId, academicYearId, ct)
            .ConfigureAwait(false);

        IList<ClassFeeAmountItemDto> items = rows
            .Select(MapAmountItem)
            .ToList();

        bool classHasConfiguredAmounts = await _repo
            .ClassHasConfiguredAmountsAsync(classGroupId, academicYearId, versionId, ct)
            .ConfigureAwait(false);

        return Result<ClassFeeAmountsResponseDto>.Success(new ClassFeeAmountsResponseDto(
            classGroupId,
            summary?.ClassName ?? "Class",
            academicYearId,
            versionId,
            version.VersionNumber,
            FeeLabelHelper.VersionStatusLabel(version.Status),
            IsClassAmountsEditable(version.Status, classHasConfiguredAmounts),
            items.Sum(i => FeeCategoryHelper.SignedAnnualTotal(i.Category, i.AnnualTotal)),
            periods.Select(p => new ClassFeePeriodDto(
                p.PeriodIndex,
                p.Name,
                p.StartDate,
                p.EndDate)).ToList(),
            items));
    }

    public async Task<Result<ClassFeeAmountsResponseDto>> SaveClassAmountsAsync(
        Guid classId,
        SaveClassFeeAmountsRequestDto request,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || request.AcademicYearId == Guid.Empty || request.FeeStructureId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class, academic year and fee structure version are required.");
        }
        if (request.Amounts is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Fee amounts are required.");
        }

        Guid classGroupId = await _repo.ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Class not found.");
        }

        FeeStructureEntity? version = await _feeStructureRepo.GetVersionByIdAsync(request.FeeStructureId, ct).ConfigureAwait(false);
        if (version is null)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure("Fee structure version not found.");
        }

        bool classHasConfiguredAmounts = await _repo
            .ClassHasConfiguredAmountsAsync(classGroupId, request.AcademicYearId, request.FeeStructureId, ct)
            .ConfigureAwait(false);
        if (!IsClassAmountsEditable(version.Status, classHasConfiguredAmounts))
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                version.Status == FeeStructureVersionStatus.Active
                    ? "This class already has fee amounts configured on the active structure. Create a new draft version from Fee Structure to change them."
                    : "This fee structure version cannot be edited.");
        }

        IReadOnlyList<ClassAcademicPeriodEntity> periods = await _periodRepo
            .GetByClassAsync(classGroupId, request.AcademicYearId, ct)
            .ConfigureAwait(false);
        IList<ClassFeeAmountRow> feeHeads = await _repo
            .GetAmountsByClassAsync(classGroupId, request.AcademicYearId, request.FeeStructureId, ct)
            .ConfigureAwait(false);
        Dictionary<Guid, ClassFeeAmountRow> feeHeadById = feeHeads.ToDictionary(x => x.FeeHeadId);

        HashSet<int> validPeriodIndexes = periods.Select(p => p.PeriodIndex).ToHashSet();
        foreach (SaveClassFeeAmountItemDto item in request.Amounts)
        {
            if (item.Amount < 0 || (item.PeriodAmounts?.Any(p => p.Amount < 0) ?? false))
            {
                return Result<ClassFeeAmountsResponseDto>.Failure("Fee amounts cannot be negative.");
            }
            if ((item.PeriodAmounts ?? []).Any(p => !validPeriodIndexes.Contains(p.PeriodIndex)))
            {
                return Result<ClassFeeAmountsResponseDto>.Failure("A fee amount references an invalid academic period.");
            }
            if (feeHeadById.TryGetValue(item.FeeHeadId, out ClassFeeAmountRow? feeHead)
                && (FeeCollectionType)feeHead.CollectionType == FeeCollectionType.PeriodWise)
            {
                if (periods.Count == 0)
                {
                    return Result<ClassFeeAmountsResponseDto>.Failure(
                        "Configure academic periods for this class before setting period-wise fee amounts.");
                }
                HashSet<int> submittedIndexes = (item.PeriodAmounts ?? [])
                    .Select(p => p.PeriodIndex)
                    .ToHashSet();
                if (!submittedIndexes.SetEquals(validPeriodIndexes))
                {
                    return Result<ClassFeeAmountsResponseDto>.Failure(
                        $"Enter an amount for every academic period for fee head '{feeHead.FeeHeadName}'.");
                }
            }
        }

        IList<ClassFeeAmountUpsertRow> amounts = request.Amounts
            .Select(a => new ClassFeeAmountUpsertRow
            {
                FeeHeadId = a.FeeHeadId,
                Amount = feeHeadById.TryGetValue(a.FeeHeadId, out ClassFeeAmountRow? amountFeeHead)
                    && (FeeCollectionType)amountFeeHead.CollectionType == FeeCollectionType.PeriodWise
                    ? (a.PeriodAmounts ?? []).Sum(p => p.Amount)
                    : a.Amount,
                PeriodAmounts = feeHeadById.TryGetValue(a.FeeHeadId, out ClassFeeAmountRow? feeHead)
                    && (FeeCollectionType)feeHead.CollectionType == FeeCollectionType.PeriodWise
                    ? (a.PeriodAmounts ?? [])
                    .Select(p => new ClassFeePeriodAmountRow
                    {
                        FeeHeadId = a.FeeHeadId,
                        PeriodIndex = p.PeriodIndex,
                        Amount = p.Amount,
                    })
                    .ToList()
                    : [],
            })
            .ToList();

        await _repo.UpsertAmountsAsync(classGroupId, request.AcademicYearId, request.FeeStructureId, amounts, ct).ConfigureAwait(false);

        if (await _installmentRepo.IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            await _installmentService
                .RegenerateForClassVersionAsync(classGroupId, request.FeeStructureId, request.AcademicYearId, ct)
                .ConfigureAwait(false);
        }

        return await GetClassAmountsAsync(classGroupId, request.AcademicYearId, request.FeeStructureId, ct).ConfigureAwait(false);
    }

    public async Task<Result<IList<ClassFeeInstallmentPreviewDto>>> GetInstallmentPreviewAsync(
        Guid classId,
        Guid academicYearId,
        Guid? feeStructureId,
        CancellationToken ct = default)
    {
        if (classId == Guid.Empty || academicYearId == Guid.Empty)
        {
            return Result<IList<ClassFeeInstallmentPreviewDto>>.Failure("Class and academic year are required.");
        }

        Guid classGroupId = await _repo.ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return Result<IList<ClassFeeInstallmentPreviewDto>>.Failure("Class not found.");
        }

        Guid versionId = await ResolveVersionIdAsync(academicYearId, feeStructureId, ct).ConfigureAwait(false);
        if (versionId == Guid.Empty)
        {
            return Result<IList<ClassFeeInstallmentPreviewDto>>.Failure("Fee structure version not found.");
        }

        await _installmentService
            .EnsureMissingInstallmentsForClassVersionAsync(classGroupId, versionId, academicYearId, ct)
            .ConfigureAwait(false);

        IList<ClassFeeInstallmentRow> rows = await _installmentRepo
            .GetByClassVersionAsync(classGroupId, versionId, ct)
            .ConfigureAwait(false);
        IList<ClassFeeInstallmentPreviewDto> dtos = rows
            .Select(r => new ClassFeeInstallmentPreviewDto(
                r.Id,
                r.FeeHeadId,
                r.FeeHeadName,
                FeeLabelHelper.CollectionTypeLabel((FeeCollectionType)r.CollectionType),
                r.PeriodIndex,
                r.PeriodLabel,
                r.PeriodStart,
                r.PeriodEnd,
                r.Amount))
            .ToList();
        return Result<IList<ClassFeeInstallmentPreviewDto>>.Success(dtos);
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

        FeeStructureEntity? admissionVersion = await _feeStructureRepo
            .GetAdmissionFeeStructureAsync(ct)
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

        bool classHasConfiguredAmounts = await _repo
            .ClassHasConfiguredAmountsAsync(result.Value.ClassId, academicYearId, admissionVersion.Id, ct)
            .ConfigureAwait(false);
        if (!classHasConfiguredAmounts)
        {
            return Result<ClassFeeAmountsResponseDto>.Failure(
                "Set class-wise fee amounts for this class before admitting students.");
        }

        return result;
    }

    private static bool IsAdmissionFeeStructureStatus(FeeStructureVersionStatus status) =>
        status is FeeStructureVersionStatus.Published or FeeStructureVersionStatus.Active;

    /// <summary>
    /// Draft and Published: full edit. Active: only classes not yet configured (e.g. newly added classes).
    /// </summary>
    private static bool IsClassAmountsEditable(FeeStructureVersionStatus status, bool classHasConfiguredAmounts) =>
        status switch
        {
            FeeStructureVersionStatus.Draft => true,
            FeeStructureVersionStatus.Published => true,
            FeeStructureVersionStatus.Active => !classHasConfiguredAmounts,
            _ => false
        };

    /// <summary>
    /// When no version is specified (e.g. student admission preview), use active or latest published — never draft.
    /// </summary>
    private async Task<Guid> ResolveVersionIdAsync(Guid academicYearId, Guid? feeStructureId, CancellationToken ct)
    {
        if (feeStructureId.HasValue && feeStructureId.Value != Guid.Empty)
        {
            return feeStructureId.Value;
        }

        FeeStructureEntity? admissionVersion = await _feeStructureRepo
            .GetAdmissionFeeStructureAsync(ct)
            .ConfigureAwait(false);
        return admissionVersion?.Id ?? Guid.Empty;
    }

    private static ClassFeeAmountItemDto MapAmountItem(ClassFeeAmountRow r)
    {
        var collectionType = (FeeCollectionType)r.CollectionType;
        decimal annualTotal = collectionType == FeeCollectionType.PeriodWise
            ? r.PeriodAmounts.Sum(p => p.Amount)
            : r.Amount;
        var category = (FeeCategory)r.Category;
        return new ClassFeeAmountItemDto(
            r.FeeHeadId,
            r.FeeHeadName,
            category,
            FeeLabelHelper.CategoryLabel(category),
            collectionType,
            FeeLabelHelper.CollectionTypeLabel(collectionType),
            r.Amount,
            r.PeriodAmounts
                .OrderBy(p => p.PeriodIndex)
                .Select(p => new ClassFeePeriodAmountDto(p.PeriodIndex, p.Amount))
                .ToList(),
            annualTotal,
            r.IsMandatory,
            r.StudentWiseDifferentAmount);
    }
}
