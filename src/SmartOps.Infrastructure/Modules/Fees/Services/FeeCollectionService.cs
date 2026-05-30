using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Application.Modules.Student.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Modules.Student;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class FeeCollectionService : IFeeCollectionService
{
    private readonly IFeeCollectionRepository _collectionRepo;
    private readonly IFeeStructureRepository _structureRepo;
    private readonly IClassFeeInstallmentRepository _installmentRepo;
    private readonly IStudentFeeHeadAssignmentRepository _feeHeadAssignmentRepo;
    private readonly IStudentFeeInstallmentRepository _studentInstallmentRepo;
    private readonly IUserScopeContext _scope;

    public FeeCollectionService(
        IFeeCollectionRepository collectionRepo,
        IFeeStructureRepository structureRepo,
        IClassFeeInstallmentRepository installmentRepo,
        IStudentFeeHeadAssignmentRepository feeHeadAssignmentRepo,
        IStudentFeeInstallmentRepository studentInstallmentRepo,
        IUserScopeContext scope)
    {
        _collectionRepo = collectionRepo;
        _structureRepo = structureRepo;
        _installmentRepo = installmentRepo;
        _feeHeadAssignmentRepo = feeHeadAssignmentRepo;
        _studentInstallmentRepo = studentInstallmentRepo;
        _scope = scope;
    }

    public async Task<Result<IList<FeeCollectionStudentListItemDto>>> GetStudentsAsync(
        Guid? classId,
        Guid? academicYearId,
        string? search,
        string? statusFilter,
        CancellationToken ct = default)
    {
        Guid yearId = await ResolveAcademicYearIdAsync(academicYearId, ct).ConfigureAwait(false);
        if (yearId == Guid.Empty)
        {
            return Result<IList<FeeCollectionStudentListItemDto>>.Failure("Academic year is required.");
        }

        IList<FeeCollectionStudentRow> rows = await _collectionRepo
            .GetStudentsAsync(classId, yearId, search, statusFilter, ct)
            .ConfigureAwait(false);

        await EnsureInstallmentsForStudentRowsAsync(rows, yearId, ct).ConfigureAwait(false);
        rows = await _collectionRepo
            .GetStudentsAsync(classId, yearId, search, statusFilter, ct)
            .ConfigureAwait(false);

        IList<FeeCollectionStudentListItemDto> items = rows.Select(MapListItem).ToList();
        return Result<IList<FeeCollectionStudentListItemDto>>.Success(items);
    }

    public async Task<Result<FeeCollectionStudentDetailDto>> GetStudentDetailAsync(
        Guid studentId,
        Guid? academicYearId,
        CancellationToken ct = default)
    {
        Guid yearId = await ResolveAcademicYearIdAsync(academicYearId, ct).ConfigureAwait(false);
        if (yearId == Guid.Empty)
        {
            return Result<FeeCollectionStudentDetailDto>.Failure("Academic year is required.");
        }

        FeeCollectionStudentRow? row = await _collectionRepo.GetStudentRowAsync(studentId, yearId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<FeeCollectionStudentDetailDto>.Failure("Student not found.");
        }

        row = await EnsureStudentVersionAssignedAsync(row, yearId, ct).ConfigureAwait(false);
        if (row is null || row.FeeStructureVersionId == Guid.Empty)
        {
            return Result<FeeCollectionStudentDetailDto>.Failure("No fee structure version is assigned to this student.");
        }

        FeeCollectionStudentDetailDto detail = await BuildStudentDetailAsync(row, yearId, ct).ConfigureAwait(false);
        return Result<FeeCollectionStudentDetailDto>.Success(detail);
    }

    public async Task<Result<CollectFeeResponseDto>> CollectFeeAsync(CollectFeeRequestDto request, CancellationToken ct = default)
    {
        if (request.StudentId == Guid.Empty)
        {
            return Result<CollectFeeResponseDto>.Failure("Student is required.");
        }

        if (request.Amount <= 0)
        {
            return Result<CollectFeeResponseDto>.Failure("Amount must be greater than zero.");
        }

        Guid yearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, ct).ConfigureAwait(false);
        if (yearId == Guid.Empty)
        {
            return Result<CollectFeeResponseDto>.Failure("Academic year is required.");
        }

        FeeCollectionStudentRow? studentRow =
            await _collectionRepo.GetStudentRowAsync(request.StudentId, yearId, ct).ConfigureAwait(false);
        if (studentRow is null)
        {
            return Result<CollectFeeResponseDto>.Failure("Student not found.");
        }

        studentRow = await EnsureStudentVersionAssignedAsync(studentRow, yearId, ct).ConfigureAwait(false);
        if (studentRow is null || studentRow.FeeStructureVersionId == Guid.Empty)
        {
            return Result<CollectFeeResponseDto>.Failure("No fee structure version is assigned to this student.");
        }

        FeeCollectionStudentDetailDto currentDetail =
            await BuildStudentDetailAsync(studentRow, yearId, ct).ConfigureAwait(false);

        decimal dueAmount = currentDetail.DueAmount;
        if (dueAmount <= 0)
        {
            return Result<CollectFeeResponseDto>.Failure("No due amount remaining for this student.");
        }

        if (request.Amount > dueAmount)
        {
            return Result<CollectFeeResponseDto>.Failure($"Amount cannot exceed due balance of {dueAmount:N2}.");
        }

        IList<FeeAllocationHelper.InstallmentDue> openInstallments = currentDetail.FeeHeads
            .SelectMany(h => h.Installments)
            .Where(i => i.DueAmount > 0)
            .Select(i => new FeeAllocationHelper.InstallmentDue(i.InstallmentId, i.FeeTypeId, i.DueAmount))
            .ToList();

        HashSet<Guid> selectedInstallmentIds = request.Allocations
            .Where(a => a.InstallmentId.HasValue && a.InstallmentId.Value != Guid.Empty)
            .Select(a => a.InstallmentId!.Value)
            .ToHashSet();

        IList<(Guid FeeTypeId, Guid? InstallmentId, decimal Amount)> allocations;

        if (openInstallments.Count > 0)
        {
            if (selectedInstallmentIds.Count == 0)
            {
                foreach (FeeAllocationHelper.InstallmentDue inst in openInstallments)
                {
                    selectedInstallmentIds.Add(inst.InstallmentId);
                }
            }

            decimal selectedDue = FeeAllocationHelper.SumDueOnSelectedInstallments(openInstallments, selectedInstallmentIds);
            if (selectedDue <= 0 && openInstallments.Sum(i => i.DueAmount) > 0)
            {
                selectedInstallmentIds = openInstallments
                    .Select(i => i.InstallmentId)
                    .ToHashSet();
                selectedDue = openInstallments.Sum(i => i.DueAmount);
            }

            if (selectedDue <= 0)
            {
                return Result<CollectFeeResponseDto>.Failure(
                    "Selected installments have no remaining due. Refresh the student and try again.");
            }

            decimal maxCollectOnSelection = Math.Min(selectedDue, dueAmount);
            if (request.Amount > maxCollectOnSelection)
            {
                return Result<CollectFeeResponseDto>.Failure(
                    $"Amount cannot exceed {maxCollectOnSelection:N2} due on the selected installments.");
            }

            foreach (Guid installmentId in selectedInstallmentIds)
            {
                bool validStudent = await _studentInstallmentRepo
                    .InstallmentBelongsToStudentAsync(
                        installmentId,
                        studentRow.StudentId,
                        studentRow.FeeStructureVersionId,
                        ct)
                    .ConfigureAwait(false);
                bool validClass = !validStudent && await _installmentRepo.InstallmentBelongsToClassVersionAsync(
                    installmentId,
                    studentRow.ClassId,
                    studentRow.FeeStructureVersionId,
                    ct).ConfigureAwait(false);
                if (!validStudent && !validClass)
                {
                    return Result<CollectFeeResponseDto>.Failure("Invalid installment selection.");
                }
            }

            allocations = FeeAllocationHelper.AllocateToSelectedInstallments(
                    openInstallments,
                    request.Amount,
                    selectedInstallmentIds)
                .Select(a => (a.FeeTypeId, (Guid?)a.InstallmentId, a.Amount))
                .ToList();
        }
        else
        {
            allocations = await CollectLegacyAsync(request, studentRow, ct).ConfigureAwait(false);
        }

        if (allocations.Count == 0)
        {
            return Result<CollectFeeResponseDto>.Failure("Could not allocate payment.");
        }

        (Guid paymentId, string receiptNo) = await _collectionRepo.CreatePaymentAsync(
            request.StudentId,
            studentRow.FeeStructureVersionId,
            request.Amount,
            request.PaymentMode,
            request.TransactionNo,
            request.PaymentDate,
            request.Remarks,
            allocations,
            ct).ConfigureAwait(false);

        FeeCollectionStudentRow? row = await _collectionRepo.GetStudentRowAsync(request.StudentId, yearId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<CollectFeeResponseDto>.Failure("Student not found after payment.");
        }

        FeeCollectionStudentDetailDto detail = await BuildStudentDetailAsync(row, yearId, ct).ConfigureAwait(false);
        return Result<CollectFeeResponseDto>.Success(new CollectFeeResponseDto(paymentId, receiptNo, detail));
    }

    private async Task<IList<(Guid FeeTypeId, Guid? InstallmentId, decimal Amount)>> CollectLegacyAsync(
        CollectFeeRequestDto request,
        FeeCollectionStudentRow studentRow,
        CancellationToken ct)
    {
        IList<StudentClassFeeAmountRow> feeAmounts = await _collectionRepo
            .GetStudentFeeAmountsAsync(studentRow.ClassId, studentRow.FeeStructureVersionId, studentRow.StudentId, ct)
            .ConfigureAwait(false);

        var heads = feeAmounts
            .Where(f => f.Amount > 0)
            .Select(f => new FeeAllocationHelper.HeadAmount(f.FeeTypeId, f.Amount))
            .ToList();

        decimal currentPaid = await _collectionRepo
            .GetStudentPaidTotalAsync(request.StudentId, studentRow.FeeStructureVersionId, ct)
            .ConfigureAwait(false);
        IList<FeeAllocationHelper.HeadAllocation> distributed =
            FeeAllocationHelper.DistributePaid(heads, currentPaid);

        HashSet<Guid> selectedFeeTypeIds = request.Allocations
            .Where(a => a.FeeTypeId != Guid.Empty)
            .Select(a => a.FeeTypeId)
            .ToHashSet();

        if (selectedFeeTypeIds.Count == 0)
        {
            foreach (FeeAllocationHelper.HeadAllocation h in distributed)
            {
                if (h.DueAmount > 0)
                {
                    selectedFeeTypeIds.Add(h.FeeTypeId);
                }
            }
        }

        IList<(Guid FeeTypeId, decimal Amount)> legacy = FeeAllocationHelper.AllocateToSelectedHeads(
            distributed,
            request.Amount,
            selectedFeeTypeIds);

        return legacy.Select(a => (a.FeeTypeId, (Guid?)null, a.Amount)).ToList();
    }

    private async Task<FeeCollectionStudentRow?> EnsureStudentVersionAssignedAsync(
        FeeCollectionStudentRow row,
        Guid academicYearId,
        CancellationToken ct)
    {
        if (row.FeeStructureVersionId != Guid.Empty)
        {
            return row;
        }

        Guid? versionId = await _collectionRepo.GetStudentFeeStructureVersionHintAsync(row.StudentId, ct).ConfigureAwait(false);
        if (!versionId.HasValue || versionId.Value == Guid.Empty)
        {
            FeeStructureVersionEntity? admission = await _structureRepo
                .GetAdmissionVersionForYearAsync(academicYearId, ct)
                .ConfigureAwait(false);
            versionId = admission?.Id;
        }

        if (!versionId.HasValue || versionId.Value == Guid.Empty)
        {
            return row;
        }

        await _collectionRepo.AssignStudentFeeStructureVersionAsync(row.StudentId, academicYearId, versionId.Value, ct)
            .ConfigureAwait(false);
        return await _collectionRepo.GetStudentRowAsync(row.StudentId, academicYearId, ct).ConfigureAwait(false);
    }

    private async Task<Guid> ResolveAcademicYearIdAsync(Guid? academicYearId, CancellationToken ct)
    {
        if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
        {
            return academicYearId.Value;
        }

        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        if (_scope.ActiveAcademicYearId.HasValue)
        {
            return _scope.ActiveAcademicYearId.Value;
        }

        FeeSettingsEntity? settings = await _structureRepo.GetSettingsAsync(ct).ConfigureAwait(false);
        return settings?.DefaultAcademicYearId ?? Guid.Empty;
    }

    private async Task<FeeCollectionStudentDetailDto> BuildStudentDetailAsync(
        FeeCollectionStudentRow row,
        Guid academicYearId,
        CancellationToken ct)
    {
        await RepairStudentFeesIfNeededAsync(row, academicYearId, ct).ConfigureAwait(false);

        IList<ClassFeeInstallmentRow> installmentRows;
        if (await _studentInstallmentRepo
                .StudentHasInstallmentsAsync(row.StudentId, row.FeeStructureVersionId, ct)
                .ConfigureAwait(false))
        {
            installmentRows = await _studentInstallmentRepo
                .GetByStudentVersionAsync(row.StudentId, row.FeeStructureVersionId, ct)
                .ConfigureAwait(false);
        }
        else
        {
            installmentRows = await _installmentRepo
                .GetByClassVersionAsync(row.ClassId, row.FeeStructureVersionId, ct)
                .ConfigureAwait(false);
            installmentRows = await FilterInstallmentsByStudentSelectionAsync(
                    row.StudentId,
                    row.FeeStructureVersionId,
                    installmentRows,
                    ct)
                .ConfigureAwait(false);
        }
        IList<InstallmentPaidRow> paidRows = await _installmentRepo
            .GetPaidByInstallmentAsync(row.StudentId, row.FeeStructureVersionId, ct)
            .ConfigureAwait(false);
        IList<FeePaymentHistoryRow> payments = await _collectionRepo.GetPaymentHistoryAsync(row.StudentId, ct).ConfigureAwait(false);

        var paidByInstallment = paidRows.ToDictionary(p => p.InstallmentId, p => p.PaidAmount);

        IList<FeeCollectionHeadDto> heads;
        decimal total;
        decimal paid;

        if (installmentRows.Count > 0)
        {
            heads = BuildHeadsFromInstallments(installmentRows, paidByInstallment);
            total = heads.Sum(h => h.TotalAmount);
            paid = heads.Sum(h => h.PaidAmount);
        }
        else
        {
            (heads, total, paid) = await BuildLegacyHeadsAsync(row, ct).ConfigureAwait(false);
        }

        decimal due = Math.Max(0, total - paid);
        int pct = total > 0 ? (int)Math.Min(100, Math.Round(paid / total * 100)) : 0;

        IList<FeeCollectionPaymentHistoryDto> history = payments.Select(p => new FeeCollectionPaymentHistoryDto(
            p.PaymentId,
            p.PaymentDate,
            FeeLabelHelper.PaymentModeLabel((FeePaymentMode)p.PaymentMode),
            p.Amount,
            p.TransactionNo,
            p.FeeHeadsSummary,
            p.ReceiptNo)).ToList();

        IList<FeeCollectionSemesterStatusDto> semesterStatuses = BuildSemesterStatuses(heads);

        return new FeeCollectionStudentDetailDto(
            row.StudentId,
            row.StudentName,
            row.RollNo,
            row.ClassName,
            total,
            paid,
            due,
            pct,
            FeeLabelHelper.PaymentStatus(total, paid),
            heads,
            semesterStatuses,
            history);
    }

    private static IList<FeeCollectionSemesterStatusDto> BuildSemesterStatuses(IList<FeeCollectionHeadDto> heads)
    {
        var bySemester = new Dictionary<int, (string Name, DateOnly Start, DateOnly End, decimal Total, decimal Paid)>();

        foreach (FeeCollectionHeadDto head in heads)
        {
            foreach (FeeCollectionInstallmentDto inst in head.Installments)
            {
                if (inst.PeriodIndex <= 0)
                {
                    continue;
                }

                if (!bySemester.TryGetValue(inst.PeriodIndex, out var agg))
                {
                    agg = (inst.PeriodLabel, inst.PeriodStart, inst.PeriodEnd, 0m, 0m);
                }

                bySemester[inst.PeriodIndex] = (
                    agg.Name,
                    agg.Start == default ? inst.PeriodStart : agg.Start,
                    inst.PeriodEnd,
                    agg.Total + inst.TotalAmount,
                    agg.Paid + inst.PaidAmount);
            }
        }

        return bySemester
            .OrderBy(kv => kv.Key)
            .Select(kv =>
            {
                decimal due = Math.Max(0, kv.Value.Total - kv.Value.Paid);
                return new FeeCollectionSemesterStatusDto(
                    kv.Key,
                    kv.Value.Name,
                    kv.Value.Start,
                    kv.Value.End,
                    kv.Value.Total,
                    kv.Value.Paid,
                    due,
                    FeeAllocationHelper.StatusForPeriod(kv.Value.Total, kv.Value.Paid, kv.Value.End));
            })
            .ToList();
    }

    private static IList<FeeCollectionHeadDto> BuildHeadsFromInstallments(
        IList<ClassFeeInstallmentRow> installmentRows,
        IReadOnlyDictionary<Guid, decimal> paidByInstallment)
    {
        return installmentRows
            .GroupBy(i => i.FeeTypeId)
            .Select(g =>
            {
                ClassFeeInstallmentRow first = g.First();
                IList<FeeCollectionInstallmentDto> installments = g
                    .OrderBy(i => i.PeriodIndex)
                    .Select(i =>
                    {
                        decimal instPaid = paidByInstallment.GetValueOrDefault(i.Id, 0m);
                        bool isDiscount = FeeCategoryHelper.IsDiscount(i.Category);
                        decimal signedAmount = isDiscount
                            ? FeeCategoryHelper.SignedInstallmentAmount((FeeCategory)i.Category, i.Amount)
                            : i.Amount;
                        decimal instDue = isDiscount
                            ? signedAmount - instPaid
                            : Math.Max(0, i.Amount - instPaid);
                        return new FeeCollectionInstallmentDto(
                            i.Id,
                            i.FeeTypeId,
                            i.PeriodIndex,
                            i.PeriodLabel,
                            i.PeriodStart,
                            i.PeriodEnd,
                            signedAmount,
                            instPaid,
                            instDue,
                            FeeAllocationHelper.StatusForPeriod(signedAmount, instPaid, i.PeriodEnd));
                    })
                    .ToList();

                decimal headTotal = installments.Sum(x => x.TotalAmount);
                decimal headPaid = installments.Sum(x => x.PaidAmount);
                decimal headDue = installments.Sum(x => x.DueAmount);

                return new FeeCollectionHeadDto(
                    first.FeeTypeId,
                    first.FeeTypeName,
                    FeeLabelHelper.CollectionTypeLabel((FeeCollectionType)first.CollectionType),
                    headTotal,
                    headPaid,
                    headDue,
                    FeeAllocationHelper.StatusForHead(headTotal, headPaid),
                    installments);
            })
            .OrderBy(h => h.FeeTypeName)
            .ToList();
    }

    private async Task<(IList<FeeCollectionHeadDto> Heads, decimal Total, decimal Paid)> BuildLegacyHeadsAsync(
        FeeCollectionStudentRow row,
        CancellationToken ct)
    {
        IList<StudentClassFeeAmountRow> feeAmounts = await _collectionRepo
            .GetStudentFeeAmountsAsync(row.ClassId, row.FeeStructureVersionId, row.StudentId, ct)
            .ConfigureAwait(false);

        decimal paid = await _collectionRepo.GetStudentPaidTotalAsync(row.StudentId, row.FeeStructureVersionId, ct).ConfigureAwait(false);

        var headAmounts = feeAmounts
            .Where(f => f.Amount > 0)
            .Select(f => new FeeAllocationHelper.HeadAmount(f.FeeTypeId, f.Amount))
            .ToList();

        IList<FeeCollectionHeadDto> heads = FeeAllocationHelper
            .DistributePaid(headAmounts, paid)
            .Select(h =>
            {
                StudentClassFeeAmountRow? meta = feeAmounts.FirstOrDefault(f => f.FeeTypeId == h.FeeTypeId);
                var legacyInstallment = new FeeCollectionInstallmentDto(
                    Guid.Empty,
                    h.FeeTypeId,
                    1,
                    "Full year",
                    default,
                    default,
                    h.TotalAmount,
                    h.PaidAmount,
                    h.DueAmount,
                    FeeAllocationHelper.StatusForHead(h.TotalAmount, h.PaidAmount));

                return new FeeCollectionHeadDto(
                    h.FeeTypeId,
                    meta?.FeeTypeName ?? string.Empty,
                    FeeLabelHelper.CollectionTypeLabel((FeeCollectionType)(meta?.CollectionType ?? 0)),
                    h.TotalAmount,
                    h.PaidAmount,
                    h.DueAmount,
                    FeeAllocationHelper.StatusForHead(h.TotalAmount, h.PaidAmount),
                    new[] { legacyInstallment });
            })
            .ToList();

        decimal total = headAmounts.Sum(h => h.Amount);
        return (heads, total, paid);
    }

    private static FeeCollectionStudentListItemDto MapListItem(FeeCollectionStudentRow r)
    {
        decimal due = Math.Max(0, r.TotalFees - r.PaidAmount);
        return new FeeCollectionStudentListItemDto(
            r.StudentId,
            r.StudentName,
            r.RollNo,
            r.ClassName,
            r.TotalFees,
            r.PaidAmount,
            due,
            FeeLabelHelper.PaymentStatus(r.TotalFees, r.PaidAmount));
    }

    private async Task EnsureInstallmentsForStudentRowsAsync(
        IList<FeeCollectionStudentRow> rows,
        Guid academicYearId,
        CancellationToken ct)
    {
        foreach (var group in rows
                     .Where(r => r.FeeStructureVersionId != Guid.Empty)
                     .GroupBy(r => (r.ClassId, r.FeeStructureVersionId)))
        {
            await _installmentRepo
                .EnsureMissingInstallmentsForClassVersionAsync(
                    group.Key.ClassId,
                    group.Key.FeeStructureVersionId,
                    academicYearId,
                    ct)
                .ConfigureAwait(false);
        }

        foreach (FeeCollectionStudentRow row in rows.Where(r => r.FeeStructureVersionId != Guid.Empty))
        {
            await RepairStudentFeesIfNeededAsync(row, academicYearId, ct).ConfigureAwait(false);
        }
    }

    private async Task RepairStudentFeesIfNeededAsync(
        FeeCollectionStudentRow row,
        Guid academicYearId,
        CancellationToken ct)
    {
        if (row.FeeStructureVersionId == Guid.Empty)
        {
            return;
        }

        await _installmentRepo
            .EnsureMissingInstallmentsForClassVersionAsync(
                row.ClassId,
                row.FeeStructureVersionId,
                academicYearId,
                ct)
            .ConfigureAwait(false);

        await _studentInstallmentRepo
            .EnsureCurrentYearInstallmentsAsync(
                row.StudentId,
                row.ClassId,
                row.FeeStructureVersionId,
                academicYearId,
                ct)
            .ConfigureAwait(false);

        await TryCarryForwardPriorYearPendingAsync(row, academicYearId, ct).ConfigureAwait(false);
    }

    private async Task TryCarryForwardPriorYearPendingAsync(
        FeeCollectionStudentRow row,
        Guid targetAcademicYearId,
        CancellationToken ct)
    {
        PriorYearEnrollmentRow? prior = await _collectionRepo
            .GetLatestPriorYearEnrollmentAsync(row.StudentId, targetAcademicYearId, ct)
            .ConfigureAwait(false);
        if (prior is null || prior.FeeStructureVersionId == Guid.Empty)
        {
            return;
        }

        await _studentInstallmentRepo
            .EnsureCurrentYearInstallmentsAsync(
                row.StudentId,
                row.ClassId,
                row.FeeStructureVersionId,
                targetAcademicYearId,
                ct)
            .ConfigureAwait(false);

        IList<ClassFeeInstallmentRow> currentInstallments = await _studentInstallmentRepo
            .GetByStudentVersionAsync(row.StudentId, row.FeeStructureVersionId, ct)
            .ConfigureAwait(false);
        if (currentInstallments.Any(i => StudentFeeInstallmentRepository.IsCarriedForwardPeriodLabel(i.PeriodLabel)))
        {
            return;
        }

        FeeCollectionStudentRow? priorRow = await _collectionRepo
            .GetStudentRowAsync(row.StudentId, prior.AcademicYearId, ct)
            .ConfigureAwait(false);
        decimal total = priorRow?.TotalFees ?? 0m;
        decimal paid = priorRow?.PaidAmount ?? 0m;
        if (total <= 0 && prior.ClassId != Guid.Empty)
        {
            total = await _collectionRepo
                .GetStudentTotalFeesAsync(prior.ClassId, prior.FeeStructureVersionId, ct)
                .ConfigureAwait(false);
            if (paid == 0)
            {
                paid = await _collectionRepo
                    .GetStudentPaidTotalAsync(row.StudentId, prior.FeeStructureVersionId, ct)
                    .ConfigureAwait(false);
            }
        }

        decimal pending = Math.Max(0, total - paid);
        if (pending <= 0)
        {
            return;
        }

        await _studentInstallmentRepo
            .CopyFeeHeadAssignmentsFromVersionAsync(
                row.StudentId,
                prior.FeeStructureVersionId,
                row.FeeStructureVersionId,
                ct)
            .ConfigureAwait(false);

        await _studentInstallmentRepo
            .AddCarriedForwardBalanceAsync(
                row.StudentId,
                row.ClassId,
                row.FeeStructureVersionId,
                targetAcademicYearId,
                pending,
                ct)
            .ConfigureAwait(false);
    }

    private async Task<IList<ClassFeeInstallmentRow>> FilterInstallmentsByStudentSelectionAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        IList<ClassFeeInstallmentRow> installmentRows,
        CancellationToken ct)
    {
        if (installmentRows.Count == 0)
        {
            return installmentRows;
        }

        IReadOnlySet<Guid>? included = await _feeHeadAssignmentRepo
            .GetIncludedFeeTypeIdsAsync(studentId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        if (included is null)
        {
            return installmentRows;
        }

        return installmentRows.Where(r => included.Contains(r.FeeTypeId)).ToList();
    }
}
