namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeCollectionRepository
{
    Task<IList<FeeCollectionStudentRow>> GetStudentsAsync(
        Guid? classId,
        Guid academicYearId,
        string? search,
        string? statusFilter,
        CancellationToken ct = default);

    Task<FeeCollectionStudentRow?> GetStudentRowAsync(Guid studentId, Guid academicYearId, CancellationToken ct = default);

    Task<IList<StudentClassFeeAmountRow>> GetStudentFeeAmountsAsync(
        Guid studentId,
        Guid classId,
        Guid academicYearId,
        CancellationToken ct = default);

    Task<decimal> GetStudentPaidTotalAsync(Guid studentId, CancellationToken ct = default);

    Task<IList<StudentFeeHeadPaidRow>> GetPaidByFeeTypeAsync(Guid studentId, CancellationToken ct = default);

    Task<IList<FeePaymentHistoryRow>> GetPaymentHistoryAsync(Guid studentId, CancellationToken ct = default);

    Task<(Guid PaymentId, string ReceiptNo)> CreatePaymentAsync(
        Guid studentId,
        decimal amount,
        int paymentMode,
        string? transactionNo,
        DateOnly paymentDate,
        string? remarks,
        IList<(Guid FeeTypeId, decimal Amount)> allocations,
        CancellationToken ct = default);
}

public sealed class FeeCollectionStudentRow
{
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string RollNo { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public decimal TotalFees { get; init; }
    public decimal PaidAmount { get; init; }
}

public sealed class StudentClassFeeAmountRow
{
    public Guid FeeTypeId { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public int Frequency { get; init; }
    public decimal Amount { get; init; }
}

public sealed class StudentFeeHeadPaidRow
{
    public Guid FeeTypeId { get; init; }
    public decimal PaidAmount { get; init; }
}

public sealed class FeePaymentHistoryRow
{
    public Guid PaymentId { get; init; }
    public DateOnly PaymentDate { get; init; }
    public int PaymentMode { get; init; }
    public decimal Amount { get; init; }
    public string? TransactionNo { get; init; }
    public string? ReceiptNo { get; init; }
    public string FeeHeadsSummary { get; init; } = string.Empty;
}
