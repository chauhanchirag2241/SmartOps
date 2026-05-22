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

        IList<(Guid FeeTypeId, decimal Amount)> allocations = request.Allocations
            .Where(a => a.Amount > 0)
            .Select(a => (a.FeeTypeId, a.Amount))
            .ToList();

        if (allocations.Count == 0)
        {
            allocations = [(Guid.Empty, request.Amount)];
        }

        (Guid paymentId, string receiptNo) = await _collectionRepo.CreatePaymentAsync(
            request.StudentId,
            request.Amount,
            request.PaymentMode,
            request.TransactionNo,
            request.PaymentDate,
            request.Remarks,
            allocations.Where(a => a.FeeTypeId != Guid.Empty).ToList(),
            ct).ConfigureAwait(false);

        FeeSettingsEntity? settings = await _structureRepo.GetSettingsAsync(ct).ConfigureAwait(false);
        Guid yearId = settings?.DefaultAcademicYearId ?? Guid.Empty;
        FeeCollectionStudentRow? row = await _collectionRepo.GetStudentRowAsync(request.StudentId, yearId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<CollectFeeResponseDto>.Failure("Student not found after payment.");
        }

        FeeCollectionStudentDetailDto detail = await BuildStudentDetailAsync(row, yearId, ct).ConfigureAwait(false);
        return Result<CollectFeeResponseDto>.Success(new CollectFeeResponseDto(paymentId, receiptNo, detail));
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
        Guid academicYearId,
        CancellationToken ct)
    {
        IList<StudentClassFeeAmountRow> feeAmounts = await _collectionRepo
            .GetStudentFeeAmountsAsync(row.StudentId, row.ClassId, academicYearId, ct)
            .ConfigureAwait(false);
        IList<StudentFeeHeadPaidRow> paidByHead = await _collectionRepo.GetPaidByFeeTypeAsync(row.StudentId, ct).ConfigureAwait(false);
        IList<FeePaymentHistoryRow> payments = await _collectionRepo.GetPaymentHistoryAsync(row.StudentId, ct).ConfigureAwait(false);

        decimal total = feeAmounts.Sum(f => f.Amount);
        decimal paid = row.PaidAmount;
        decimal due = Math.Max(0, total - paid);
        int pct = total > 0 ? (int)Math.Min(100, Math.Round(paid / total * 100)) : 0;

        var paidLookup = paidByHead.ToDictionary(p => p.FeeTypeId, p => p.PaidAmount);
        IList<FeeCollectionHeadStatusDto> heads = feeAmounts.Select(f =>
        {
            decimal headPaid = paidLookup.GetValueOrDefault(f.FeeTypeId);
            decimal headDue = Math.Max(0, f.Amount - headPaid);
            string status = headDue <= 0 && f.Amount > 0 ? "Paid" : headPaid > 0 ? "Partial" : "Unpaid";
            return new FeeCollectionHeadStatusDto(
                f.FeeTypeId,
                f.FeeTypeName,
                FeeLabelHelper.FrequencyLabel((FeeFrequency)f.Frequency),
                f.Amount,
                headPaid,
                headDue,
                status);
        }).ToList();

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
