using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Salary.Interfaces;

public interface IEmployeeSalaryService
{
    Task<Result<IList<EmployeeSalaryListItemDto>>> GetEmployeesAsync(
        string? search,
        Guid? departmentId,
        string? designation,
        CancellationToken ct = default);

    Task<Result<EmployeeSalaryDetailDto>> GetEmployeeDetailAsync(Guid employeeid, CancellationToken ct = default);

    Task<Result<EmployeeSalaryDetailDto>> AssignOrUpdateAsync(
        Guid employeeid,
        AssignEmployeeSalaryRequestDto request,
        CancellationToken ct = default);
}
