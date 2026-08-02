using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary;

public record EmployeeSalaryComponentValueDto(
    Guid SalaryVersionComponentId,
    decimal Value);

public record EmployeeSalaryComponentItemDto(
    Guid SalaryVersionComponentId,
    string Name,
    string? ShortCode,
    SalaryComponentType ComponentType,
    string ComponentTypeLabel,
    SalaryCalculationType CalculationType,
    string CalculationTypeLabel,
    decimal Value,
    decimal DefaultValue,
    bool IsTaxable);

public record EmployeeSalaryListItemDto(
    Guid EmployeeRecordId,
    string EmployeeName,
    string? EmployeeCode,
    string? Department,
    string? Designation,
    string? EmployeeType,
    decimal? NetSalary,
    bool HasAssignment);

public record SalaryLineItemDto(
    Guid? ComponentId,
    string Name,
    SalaryComponentType ComponentType,
    string ComponentTypeLabel,
    decimal Amount,
    bool IsEarning);

public record EmployeeSalaryDetailDto(
    Guid EmployeeRecordId,
    string EmployeeName,
    string? EmployeeCode,
    string? Department,
    string? Designation,
    Guid? EmployeeSalaryId,
    Guid? SalaryStructureVersionId,
    decimal BasicSalary,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    DateOnly? EffectiveDate,
    IList<EmployeeSalaryComponentItemDto> Components,
    IList<SalaryLineItemDto> Earnings,
    IList<SalaryLineItemDto> Deductions);

public record AssignEmployeeSalaryRequestDto(
    Guid SalaryStructureVersionId,
    DateOnly EffectiveDate,
    IList<EmployeeSalaryComponentValueDto> Components);

public record PayrollRunDto(
    Guid Id,
    int PayYear,
    int PayMonth,
    PayrollRunStatus Status,
    string StatusLabel,
    bool UseAttendanceWiseSalary,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    int EmployeeCount,
    DateTime? ProcessedOn,
    IList<PayrollEntryListItemDto> Entries);

public record PayrollEntryListItemDto(
    Guid Id,
    Guid EmployeeRecordId,
    string EmployeeName,
    string? Department,
    decimal BasicSalary,
    decimal HraAmount,
    decimal Allowances,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    int WorkingDays,
    int PresentDays,
    int DaysCut,
    decimal AttendanceCutAmount,
    decimal PerDayCutAmount,
    bool UseFullSalaryOverride,
    PayrollEntryStatus Status,
    string StatusLabel,
    IList<SalaryLineItemDto> Earnings,
    IList<SalaryLineItemDto> Deductions);

public record ProcessPayrollRequestDto(
    int PayYear,
    int PayMonth,
    bool UseAttendanceWiseSalary,
    IList<Guid>? FullSalaryEmployeeIds = null);

public record PreviewPayrollRequestDto(
    int PayYear,
    int PayMonth,
    bool UseAttendanceWiseSalary,
    IList<Guid>? FullSalaryEmployeeIds = null);

public record MarkPayrollPaidRequestDto(
    IList<Guid>? EntryIds);

public record PayslipDto(
    Guid EntryId,
    int PayYear,
    int PayMonth,
    string EmployeeName,
    string? EmployeeCode,
    string? Department,
    string? Designation,
    bool UseAttendanceWiseSalary,
    int WorkingDays,
    int PresentDays,
    int DaysCut,
    decimal AttendanceCutAmount,
    decimal BasicSalary,
    decimal GrossSalary,
    decimal TotalDeductions,
    decimal NetSalary,
    string? BankName,
    string? BankAccountNumber,
    string? BankIfscCode,
    IList<SalaryLineItemDto> Earnings,
    IList<SalaryLineItemDto> Deductions);
