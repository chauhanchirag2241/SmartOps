using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Salary;

public class SalaryStructureVersionEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public int VersionNumber { get; set; }
    public SalaryStructureVersionStatus Status { get; set; } = SalaryStructureVersionStatus.Draft;
    public DateOnly? EffectiveDate { get; set; }
    public DateTime? PublishedOn { get; set; }
    public DateTime? ActivatedOn { get; set; }
}

public class SalaryVersionComponentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid SalaryStructureVersionId { get; set; }
    public string Name { get; set; } = null!;
    public string? ShortCode { get; set; }
    public SalaryComponentType ComponentType { get; set; }
    public SalaryCalculationType CalculationType { get; set; }
    public decimal Value { get; set; }
    public bool IsTaxable { get; set; }
}

public class EmployeeSalaryEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SalaryStructureVersionId { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public class EmployeeSalaryComponentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeSalaryId { get; set; }
    public Guid SalaryVersionComponentId { get; set; }
    public decimal Value { get; set; }
}

public class PayrollRunEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public int PayYear { get; set; }
    public int PayMonth { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public bool UseAttendanceWiseSalary { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public int EmployeeCount { get; set; }
    public DateTime? ProcessedOn { get; set; }
}

public class PayrollEntryEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid TeacherId { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public PayrollEntryStatus Status { get; set; } = PayrollEntryStatus.Draft;
    public int WorkingDays { get; set; }
    public int PresentDays { get; set; }
}

public class PayrollEntryLineEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid PayrollEntryId { get; set; }
    public Guid? SalaryVersionComponentId { get; set; }
    public string ComponentName { get; set; } = null!;
    public SalaryComponentType ComponentType { get; set; }
    public decimal Amount { get; set; }
    public bool IsEarning { get; set; }
}
