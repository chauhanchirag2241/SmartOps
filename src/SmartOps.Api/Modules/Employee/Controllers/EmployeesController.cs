using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Employee;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Employee;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Employee.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class EmployeesController(
    IEmployeeRepository employeeRepository,
    IUserProvisioningService userProvisioning,
    IUserScopeService userScopeService,
    IResourceAuthorizationService resourceAuthorization,
    ITenantProvider tenantProvider,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Employees.Add)]
    [ProducesResponseType(typeof(CreateEmployeeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateEmployeeResponse>> CreateEmployee(
        [FromBody] CreateEmployeeDto request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest("Employee data is required.");

        var entity = request.ToEntity();
        if (entity.ReportingManagerId == entity.Id)
        {
            return BadRequest("Reporting manager cannot be the same employee.");
        }

        var employeeId = await employeeRepository.CreateEmployeeAsync(entity, cancellationToken).ConfigureAwait(false);

        if (TryGetSchoolId(out Guid schoolId))
        {
            Guid? provisionedUserId = await userProvisioning
                .ProvisionEmployeeUserAsync(entity, schoolId, cancellationToken)
                .ConfigureAwait(false);

            if (provisionedUserId.HasValue)
            {
                await employeeRepository
                    .SetEmployeeUserIdAsync(employeeId, provisionedUserId.Value, cancellationToken)
                    .ConfigureAwait(false);

                await userScopeService
                    .BumpScopeVersionAsync(provisionedUserId.Value, schoolId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return Ok(new CreateEmployeeResponse("Employee created successfully", employeeId));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<IActionResult> GetAllEmployees(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchQuery = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] StaffFilter filter = StaffFilter.All,
        CancellationToken cancellationToken = default)
    {
        var result = await employeeRepository
            .GetAllEmployeesAsync(pageIndex, pageSize, searchQuery, sortColumn, sortDirection, filter, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("/api/employee/class-teacher-dropdown")]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<IActionResult> GetClassTeacherDropdown(CancellationToken cancellationToken)
    {
        var result = await employeeRepository.GetClassTeacherDropdownAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("reporting-manager-dropdown")]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<IActionResult> GetReportingManagerDropdown(CancellationToken cancellationToken)
    {
        var result = await employeeRepository.GetReportingManagerDropdownAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<ActionResult<EmployeeEntity>> GetEmployeeById(Guid id, CancellationToken cancellationToken)
    {
        if (!await resourceAuthorization.CanAccessEmployeeAsync(id, AccessLevel.View, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        var employee = await employeeRepository.GetEmployeeByIdAsync(id, cancellationToken, includeInactive: true).ConfigureAwait(false);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Employees.Edit)]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] EmployeeEntity employee, CancellationToken cancellationToken)
    {
        if (id != employee.Id) return BadRequest("Route id and payload id must match.");

        if (!await resourceAuthorization.CanAccessEmployeeAsync(id, AccessLevel.Edit, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        if (employee.ReportingManagerId == employee.Id)
        {
            return BadRequest("Reporting manager cannot be the same employee.");
        }

        if (employee.UserTypeCode is not "TEACHER" and not "HOD")
        {
            employee.ClassId = null;
        }

        await employeeRepository.UpdateEmployeeAsync(employee, cancellationToken).ConfigureAwait(false);

        if (TryGetSchoolId(out Guid schoolId))
        {
            Guid? provisionedUserId = await userProvisioning
                .ProvisionEmployeeUserAsync(employee, schoolId, cancellationToken)
                .ConfigureAwait(false);

            if (provisionedUserId.HasValue && employee.UserId != provisionedUserId)
            {
                await employeeRepository
                    .SetEmployeeUserIdAsync(employee.Id, provisionedUserId.Value, cancellationToken)
                    .ConfigureAwait(false);

                await userScopeService
                    .BumpScopeVersionAsync(provisionedUserId.Value, schoolId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Employees.Edit)]
    public async Task<IActionResult> DeleteEmployee(Guid id, CancellationToken cancellationToken)
    {
        await employeeRepository.DeleteEmployeeAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableEmployees, id, page, pageSize, cancellationToken);

        return Ok(result);
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
