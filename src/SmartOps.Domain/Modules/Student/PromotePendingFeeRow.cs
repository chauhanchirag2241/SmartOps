namespace SmartOps.Domain.Modules.Student;

public sealed record PromotePendingFeeRow(
    Guid StudentId,
    string StudentName,
    decimal TotalFees,
    decimal PaidAmount,
    decimal PendingAmount);
