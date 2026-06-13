using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Department;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Department.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DepartmentsController(IDepartmentRepository departmentRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = MenuPolicies.Employees.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var departments = await departmentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(departments);
    }
}
