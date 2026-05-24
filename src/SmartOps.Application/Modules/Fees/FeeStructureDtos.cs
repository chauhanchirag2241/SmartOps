using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public record FeeStructureVersionListItemDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearTitle,
    int VersionNumber,
    FeeStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    int FeeTypeCount,
    bool HasStudentPayments,
    bool IsLocked);

public record FeeStructureVersionDetailDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearTitle,
    int VersionNumber,
    FeeStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    bool HasStudentPayments,
    bool IsLocked,
    IList<FeeTypeDto> FeeTypes);

public record CreateFeeStructureVersionRequestDto(
    Guid AcademicYearId,
    DateOnly? EffectiveDate,
    Guid? CloneFromVersionId);

public record FeeTypeDto(
    Guid Id,
    Guid FeeStructureVersionId,
    string Name,
    FeeCategory Category,
    string CategoryLabel,
    FeeFrequency Frequency,
    string FrequencyLabel,
    FeeAmountBasis AmountBasis,
    string AmountBasisLabel,
    bool IsMandatory,
    bool IsRefundable,
    bool IsActive,
    bool HasStudentPayments);

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
    Guid FeeStructureVersionId,
    string Name,
    FeeCategory Category,
    FeeFrequency Frequency,
    FeeAmountBasis AmountBasis,
    bool IsMandatory,
    bool IsRefundable);

public record UpdateFeeTypeRequestDto(
    string Name,
    FeeCategory Category,
    FeeFrequency Frequency,
    FeeAmountBasis AmountBasis,
    bool IsMandatory,
    bool IsRefundable);
