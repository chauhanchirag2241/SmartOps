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
    FeeCollectionType CollectionType,
    string CollectionTypeLabel,
    bool IsMandatory,
    bool IsRefundable,
    bool StudentWiseDifferentAmount,
    bool IsActive,
    bool HasStudentPayments);

public record FeeSettingsDto(
    Guid Id,
    decimal LateFeePerDay,
    Guid? DefaultAcademicYearId);

public record UpsertFeeSettingsRequestDto(
    decimal LateFeePerDay,
    Guid? DefaultAcademicYearId);

public record CreateFeeTypeRequestDto(
    Guid FeeStructureVersionId,
    string Name,
    FeeCategory Category,
    FeeCollectionType CollectionType,
    bool IsMandatory,
    bool IsRefundable,
    bool StudentWiseDifferentAmount);

public record UpdateFeeTypeRequestDto(
    string Name,
    FeeCategory Category,
    FeeCollectionType CollectionType,
    bool IsMandatory,
    bool IsRefundable,
    bool StudentWiseDifferentAmount);
