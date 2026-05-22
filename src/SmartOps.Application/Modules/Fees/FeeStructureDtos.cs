using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public record FeeTypeDto(
    Guid Id,
    string Name,
    FeeCategory Category,
    string CategoryLabel,
    FeeFrequency Frequency,
    string FrequencyLabel,
    bool IsMandatory,
    bool IsRefundable,
    bool IsActive);

public record FeeStructureStatsDto(
    int FeeTypeCount,
    int ClassesConfigured,
    string PaymentCycleLabel,
    decimal LateFeePerDay);

public record FeeSettingsDto(
    Guid Id,
    FeePaymentCycle PaymentCycle,
    string PaymentCycleLabel,
    decimal LateFeePerDay,
    Guid? DefaultAcademicYearId);

public record UpsertFeeSettingsRequestDto(
    FeePaymentCycle PaymentCycle,
    decimal LateFeePerDay,
    Guid? DefaultAcademicYearId);

public record CreateFeeTypeRequestDto(
    string Name,
    FeeCategory Category,
    FeeFrequency Frequency,
    bool IsMandatory,
    bool IsRefundable);

public record UpdateFeeTypeRequestDto(
    string Name,
    FeeCategory Category,
    FeeFrequency Frequency,
    bool IsMandatory,
    bool IsRefundable);

public record ToggleFeeTypeActiveRequestDto(bool IsActive);
