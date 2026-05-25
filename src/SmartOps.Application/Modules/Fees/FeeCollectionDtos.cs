namespace SmartOps.Application.Modules.Fees;

public record FeeCollectionStudentListItemDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    string ClassName,
    decimal TotalFees,
    decimal PaidAmount,
    decimal DueAmount,
    string PaymentStatus);

public record FeeCollectionPaymentHistoryDto(
    Guid PaymentId,
    DateOnly PaymentDate,
    string PaymentModeLabel,
    decimal Amount,
    string? TransactionNo,
    string FeeHeadsSummary,
    string? ReceiptNo);

public record FeeCollectionInstallmentDto(
    Guid InstallmentId,
    Guid FeeTypeId,
    int PeriodIndex,
    string PeriodLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string Status);

public record FeeCollectionHeadDto(
    Guid FeeTypeId,
    string FeeTypeName,
    string CollectionTypeLabel,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string Status,
    IList<FeeCollectionInstallmentDto> Installments);

/// <summary>Legacy flat head row — kept for backward-compatible API clients.</summary>
public record FeeCollectionHeadStatusDto(
    Guid FeeTypeId,
    string FeeTypeName,
    string CollectionTypeLabel,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string Status);

public record FeeCollectionSemesterStatusDto(
    int SemesterIndex,
    string SemesterName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string Status);

public record FeeCollectionStudentDetailDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    string ClassName,
    decimal TotalFees,
    decimal PaidAmount,
    decimal DueAmount,
    int PaymentProgressPercent,
    string PaymentStatus,
    IList<FeeCollectionHeadDto> FeeHeads,
    IList<FeeCollectionSemesterStatusDto> SemesterStatuses,
    IList<FeeCollectionPaymentHistoryDto> Payments);

public record CollectFeeRequestDto(
    Guid StudentId,
    decimal Amount,
    int PaymentMode,
    string? TransactionNo,
    DateOnly PaymentDate,
    string? Remarks,
    IList<CollectFeeAllocationDto> Allocations,
    Guid? AcademicYearId = null);

public record CollectFeeAllocationDto(
    Guid FeeTypeId,
    Guid? InstallmentId,
    decimal Amount);

public record CollectFeeResponseDto(
    Guid PaymentId,
    string ReceiptNo,
    FeeCollectionStudentDetailDto StudentDetail);
