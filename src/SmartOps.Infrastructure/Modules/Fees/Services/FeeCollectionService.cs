using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Infrastructure.Modules.Fees.Services;

public sealed class FeeCollectionService : IFeeCollectionService
{
    private readonly IFeeCollectionRepository _collectionRepo;
    private readonly IFeeStructureRepository _structureRepo;

    public FeeCollectionService(IFeeCollectionRepository collectionRepo, IFeeStructureRepository structureRepo)
    {
        _collectionRepo = collectionRepo;
        _structureRepo = structureRepo;
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

        FeeCollectionStudentDetailDto detail = await BuildStudentDetailAsync(row, ct).ConfigureAwait(false);
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

        decimal dueAmount = Math.Max(0, studentRow.TotalFees - studentRow.PaidAmount);
        if (dueAmount <= 0)
        {
            return Result<CollectFeeResponseDto>.Failure("No due amount remaining for this student.");
        }

        if (request.Amount > dueAmount)
        {
            return Result<CollectFeeResponseDto>.Failure($"Amount cannot exceed due balance of {dueAmount:N2}.");
        }

        IList<StudentClassFeeAmountRow> feeAmounts = await _collectionRepo
            .GetStudentFeeAmountsAsync(studentRow.ClassId, studentRow.FeeStructureVersionId, ct)
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

        decimal selectedDue = FeeAllocationHelper.SumDueOnSelected(distributed, selectedFeeTypeIds);
        if (selectedDue <= 0)
        {
            return Result<CollectFeeResponseDto>.Failure(
                "Selected fee heads have no remaining due. Refresh the student and try again.");
        }

        if (request.Amount > selectedDue)
        {
            return Result<CollectFeeResponseDto>.Failure(
                $"Amount cannot exceed {selectedDue:N2} due on the selected fee heads.");
        }

        IList<(Guid FeeTypeId, decimal Amount)> allocations = FeeAllocationHelper.AllocateToSelectedHeads(
            distributed,
            request.Amount,
            selectedFeeTypeIds);

        if (allocations.Count == 0)
        {
            return Result<CollectFeeResponseDto>.Failure("Could not allocate payment to selected fee heads.");
        }

        (Guid paymentId, string receiptNo) = await _collectionRepo.CreatePaymentAsync(
            request.StudentId,
            studentRow.FeeStructureVersionId,
            request.Amount,
            request.PaymentMode,
            request.TransactionNo,
            request.PaymentDate,
            request.Remarks,
            allocations.Where(a => a.FeeTypeId != Guid.Empty).ToList(),
            ct).ConfigureAwait(false);

        FeeCollectionStudentRow? row = await _collectionRepo.GetStudentRowAsync(request.StudentId, yearId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<CollectFeeResponseDto>.Failure("Student not found after payment.");
        }

        FeeCollectionStudentDetailDto detail = await BuildStudentDetailAsync(row, ct).ConfigureAwait(false);
        return Result<CollectFeeResponseDto>.Success(new CollectFeeResponseDto(paymentId, receiptNo, detail));
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

        FeeStructureVersionEntity? active = await _structureRepo.GetActiveVersionForYearAsync(academicYearId, ct).ConfigureAwait(false);
        if (active is null)
        {
            return row;
        }

        await _collectionRepo.AssignStudentFeeStructureVersionAsync(row.StudentId, academicYearId, active.Id, ct).ConfigureAwait(false);
        return await _collectionRepo.GetStudentRowAsync(row.StudentId, academicYearId, ct).ConfigureAwait(false);
    }

    private async Task<Guid> ResolveAcademicYearIdAsync(Guid? academicYearId, CancellationToken ct)
    {
        if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
        {
            return academicYearId.Value;
        }

        FeeSettingsEntity? settings = await _structureRepo.GetSettingsAsync(ct).ConfigureAwait(false);
        return settings?.DefaultAcademicYearId ?? Guid.Empty;
    }

    private async Task<FeeCollectionStudentDetailDto> BuildStudentDetailAsync(
        FeeCollectionStudentRow row,
        CancellationToken ct)
    {
        IList<StudentClassFeeAmountRow> feeAmounts = await _collectionRepo
            .GetStudentFeeAmountsAsync(row.ClassId, row.FeeStructureVersionId, ct)
            .ConfigureAwait(false);
        IList<FeePaymentHistoryRow> payments = await _collectionRepo.GetPaymentHistoryAsync(row.StudentId, ct).ConfigureAwait(false);

        decimal total = feeAmounts.Sum(f => f.Amount);
        decimal paid = await _collectionRepo.GetStudentPaidTotalAsync(row.StudentId, row.FeeStructureVersionId, ct).ConfigureAwait(false);
        decimal due = Math.Max(0, total - paid);
        int pct = total > 0 ? (int)Math.Min(100, Math.Round(paid / total * 100)) : 0;

        var headAmounts = feeAmounts
            .Where(f => f.Amount > 0)
            .Select(f => new FeeAllocationHelper.HeadAmount(f.FeeTypeId, f.Amount))
            .ToList();

        IList<FeeCollectionHeadStatusDto> heads = FeeAllocationHelper
            .DistributePaid(headAmounts, paid)
            .Select(h =>
            {
                StudentClassFeeAmountRow? meta = feeAmounts.FirstOrDefault(f => f.FeeTypeId == h.FeeTypeId);
                return new FeeCollectionHeadStatusDto(
                    h.FeeTypeId,
                    meta?.FeeTypeName ?? string.Empty,
                    FeeLabelHelper.FrequencyLabel((FeeFrequency)(meta?.Frequency ?? 0)),
                    h.TotalAmount,
                    h.PaidAmount,
                    h.DueAmount,
                    FeeAllocationHelper.StatusForHead(h.TotalAmount, h.PaidAmount));
            })
            .ToList();

        IList<FeeCollectionPaymentHistoryDto> history = payments.Select(p => new FeeCollectionPaymentHistoryDto(
            p.PaymentId,
            p.PaymentDate,
            FeeLabelHelper.PaymentModeLabel((FeePaymentMode)p.PaymentMode),
            p.Amount,
            p.TransactionNo,
            p.FeeHeadsSummary,
            p.ReceiptNo)).ToList();

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
            history);
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
}
