using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public record FeeStructureVersionListItemDto(
    Guid Id,
    int VersionNumber,
    FeeStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    int FeeHeadCount,
    bool HasStudentPayments,
    bool IsLocked);

public record FeeStructureVersionDetailDto(
    Guid Id,
    int VersionNumber,
    FeeStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    bool HasStudentPayments,
    bool IsLocked,
    IList<FeeHeadDto> FeeHeads);

public record CreateFeeStructureVersionRequestDto(
    DateOnly? EffectiveDate,
    Guid? CloneFromVersionId);

public record FeeHeadDto(
    Guid Id,
    Guid FeeStructureId,
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

public record CreateFeeHeadRequestDto(
    Guid FeeStructureId,
    string Name,
    FeeCategory Category,
    FeeCollectionType CollectionType,
    bool IsMandatory,
    bool IsRefundable,
    bool StudentWiseDifferentAmount);

public record UpdateFeeHeadRequestDto(
    string Name,
    FeeCategory Category,
    FeeCollectionType CollectionType,
    bool IsMandatory,
    bool IsRefundable,
    bool StudentWiseDifferentAmount);
