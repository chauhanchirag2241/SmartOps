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
    FeeCategory Category,
    string CategoryLabel,
    FeeCollectionType CollectionType,
    string CollectionTypeLabel,
    decimal Amount,
    IList<ClassFeePeriodAmountDto> PeriodAmounts,
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
    IList<ClassFeePeriodDto> Periods,
    IList<ClassFeeAmountItemDto> Items);

public record ClassFeePeriodDto(
    int PeriodIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);

public record ClassFeePeriodAmountDto(int PeriodIndex, decimal Amount);

public record SaveClassFeeAmountsRequestDto(
    Guid AcademicYearId,
    Guid FeeStructureVersionId,
    IList<SaveClassFeeAmountItemDto> Amounts);

public record SaveClassFeeAmountItemDto(
    Guid FeeTypeId,
    decimal Amount,
    IList<ClassFeePeriodAmountDto> PeriodAmounts);
