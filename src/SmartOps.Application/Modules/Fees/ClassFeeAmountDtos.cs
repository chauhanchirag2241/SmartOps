using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees;

public record ClassFeeSummaryDto(
    Guid ClassId,
    string ClassName,
    int StudentCount,
    decimal TotalAmount);

public record ClassFeeAmountItemDto(
    Guid FeeHeadId,
    string FeeHeadName,
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
    Guid FeeHeadId,
    string FeeHeadName,
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
    Guid FeeStructureId,
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
    Guid FeeStructureId,
    IList<SaveClassFeeAmountItemDto> Amounts);

public record SaveClassFeeAmountItemDto(
    Guid FeeHeadId,
    decimal Amount,
    IList<ClassFeePeriodAmountDto> PeriodAmounts);
