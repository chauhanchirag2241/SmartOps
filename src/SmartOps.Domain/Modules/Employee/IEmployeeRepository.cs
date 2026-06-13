using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Employee.Entities;

namespace SmartOps.Domain.Modules.Employee;

public interface IEmployeeRepository
{
    Task<Guid> CreateEmployeeAsync(EmployeeEntity employee, CancellationToken cancellationToken = default);
    Task<EmployeeEntity?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);
    Task<PagedResult<EmployeeListModel>> GetAllEmployeesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StaffFilter filter = StaffFilter.All,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DropdownDto>> GetClassTeacherDropdownAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DropdownDto>> GetReportingManagerDropdownAsync(CancellationToken cancellationToken = default);
    Task UpdateEmployeeAsync(EmployeeEntity employee, CancellationToken cancellationToken = default);
    Task SetEmployeeUserIdAsync(Guid employeeId, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default);
}
