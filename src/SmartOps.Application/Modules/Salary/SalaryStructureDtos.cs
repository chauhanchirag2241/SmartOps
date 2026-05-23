using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary;

public record SalaryStructureVersionListItemDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearTitle,
    int VersionNumber,
    SalaryStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    int ComponentCount,
    bool HasAssignedEmployees,
    bool IsLocked);

public record SalaryStructureVersionDetailDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearTitle,
    int VersionNumber,
    SalaryStructureVersionStatus Status,
    string StatusLabel,
    DateOnly? EffectiveDate,
    DateTime? PublishedOn,
    DateTime? ActivatedOn,
    bool HasAssignedEmployees,
    bool IsLocked,
    IList<SalaryVersionComponentDto> Components);

public record CreateSalaryStructureVersionRequestDto(
    Guid AcademicYearId,
    DateOnly? EffectiveDate,
    Guid? CloneFromVersionId);

public record SalaryVersionComponentDto(
    Guid Id,
    Guid SalaryStructureVersionId,
    string Name,
    string? ShortCode,
    SalaryComponentType ComponentType,
    string ComponentTypeLabel,
    SalaryCalculationType CalculationType,
    string CalculationTypeLabel,
    decimal Value,
    bool IsTaxable,
    bool IsActive);

public record CreateSalaryVersionComponentRequestDto(
    Guid SalaryStructureVersionId,
    string Name,
    string? ShortCode,
    SalaryComponentType ComponentType,
    SalaryCalculationType CalculationType,
    decimal Value,
    bool IsTaxable);

public record UpdateSalaryVersionComponentRequestDto(
    string Name,
    string? ShortCode,
    SalaryComponentType ComponentType,
    SalaryCalculationType CalculationType,
    decimal Value,
    bool IsTaxable);
