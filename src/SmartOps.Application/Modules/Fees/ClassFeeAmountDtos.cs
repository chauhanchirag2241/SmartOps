using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public record ClassFeeSummaryDto(
    Guid ClassId,
    string ClassName,
    int StudentCount,
    decimal TotalAmount);

public record ClassFeeAmountItemDto(
    Guid FeeTypeId,
    string FeeTypeName,
    string CategoryLabel,
    FeeCollectionType CollectionType,
    string CollectionTypeLabel,
    decimal Amount,
    decimal Semester1Amount,
    decimal Semester2Amount,
    decimal AnnualTotal,
    bool IsMandatory,
    bool StudentWiseDifferentAmount);

public record ClassFeeInstallmentPreviewDto(
    Guid InstallmentId,
    Guid FeeTypeId,
    string FeeTypeName,
    string CollectionTypeLabel,
    int PeriodIndex,
    string PeriodLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount);

public record ClassFeeAmountsResponseDto(
    Guid ClassId,
    string ClassName,
    Guid AcademicYearId,
    Guid FeeStructureVersionId,
    int VersionNumber,
    string VersionStatusLabel,
    bool IsEditable,
    decimal TotalAmount,
    IList<ClassFeeAmountItemDto> Items);

public record SaveClassFeeAmountsRequestDto(
    Guid AcademicYearId,
    Guid FeeStructureVersionId,
    IList<SaveClassFeeAmountItemDto> Amounts);

public record SaveClassFeeAmountItemDto(
    Guid FeeTypeId,
    decimal Amount,
    decimal Semester1Amount,
    decimal Semester2Amount);
