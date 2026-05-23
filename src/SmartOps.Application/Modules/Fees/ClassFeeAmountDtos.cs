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
