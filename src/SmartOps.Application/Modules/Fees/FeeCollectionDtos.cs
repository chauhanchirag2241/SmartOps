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

public record FeeCollectionHeadStatusDto(
    Guid FeeTypeId,
    string FeeTypeName,
    string FrequencyLabel,
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
    IList<FeeCollectionHeadStatusDto> FeeHeads,
    IList<FeeCollectionPaymentHistoryDto> Payments);

public record CollectFeeRequestDto(
    Guid StudentId,
    decimal Amount,
    int PaymentMode,
    string? TransactionNo,
    DateOnly PaymentDate,
    string? Remarks,
    IList<CollectFeeAllocationDto> Allocations);

public record CollectFeeAllocationDto(Guid FeeTypeId, decimal Amount);

public record CollectFeeResponseDto(
    Guid PaymentId,
    string ReceiptNo,
    FeeCollectionStudentDetailDto StudentDetail);
