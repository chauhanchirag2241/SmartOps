using SmartOps.Domain.Modules.Salary;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface IEmployeeSalaryRepository
{
    Task<IList<EmployeeSalaryListRow>> GetEmployeeSalariesAsync(
        string? search,
        Guid? departmentId,
        string? designation,
        CancellationToken ct = default);

    Task<EmployeeSalaryEntity?> GetActiveAssignmentByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);

    Task<EmployeeSalaryContextRow?> GetEmployeeSalaryContextAsync(Guid employeeId, CancellationToken ct = default);

    Task DeactivateAssignmentsForEmployeeAsync(Guid employeeId, CancellationToken ct = default);

    Task<Guid> CreateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default);

    Task UpdateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default);

    Task<IList<EmployeeSalaryComponentEntity>> GetComponentValuesForAssignmentAsync(Guid employeeSalaryId, CancellationToken ct = default);

    Task ReplaceComponentValuesAsync(Guid employeeSalaryId, IEnumerable<EmployeeSalaryComponentEntity> values, CancellationToken ct = default);

    Task<IList<EmployeeSalaryEntity>> GetActiveAssignmentsAsync(CancellationToken ct = default);
}

public sealed class EmployeeSalaryListRow
{
    public Guid EmployeeRecordId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? Department { get; init; }
    public string? Designation { get; init; }
    public Guid? EmployeeSalaryId { get; init; }
    public Guid? SalaryStructureVersionId { get; init; }
}

public sealed class EmployeeSalaryContextRow
{
    public Guid EmployeeRecordId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeId { get; init; }
    public string? Department { get; init; }
    public string? Designation { get; init; }
    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? BankIfscCode { get; init; }
    public Guid? EmployeeSalaryId { get; init; }
    public Guid? SalaryStructureVersionId { get; init; }
    public DateOnly? EffectiveDate { get; init; }
}
