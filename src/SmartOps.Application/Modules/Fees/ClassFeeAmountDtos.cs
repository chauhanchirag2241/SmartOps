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
    string FrequencyLabel,
    string AmountBasisLabel,
    decimal Amount,
    bool IsMandatory);

public record ClassFeeInstallmentPreviewDto(
    Guid InstallmentId,
    Guid FeeTypeId,
    string FeeTypeName,
    string FrequencyLabel,
    string AmountBasisLabel,
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

public record SaveClassFeeAmountItemDto(Guid FeeTypeId, decimal Amount);
