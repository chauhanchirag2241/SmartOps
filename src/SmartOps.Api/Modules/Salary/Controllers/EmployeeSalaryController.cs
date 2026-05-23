using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Salary.Controllers;

[ApiController]
[Route("api/salary/employees")]
[Authorize]
public sealed class EmployeeSalaryController : ControllerBase
{
    private readonly IEmployeeSalaryService _service;

    public EmployeeSalaryController(IEmployeeSalaryService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.SalaryEmployees.View)]
    [ProducesResponseType(typeof(IList<EmployeeSalaryListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] string? search,
        [FromQuery] Guid? departmentId,
        [FromQuery] string? designation,
        CancellationToken ct)
    {
        var result = await _service.GetEmployeesAsync(search, departmentId, designation, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{teacherId:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryEmployees.View)]
    [ProducesResponseType(typeof(EmployeeSalaryDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeDetail(Guid teacherId, CancellationToken ct)
    {
        var result = await _service.GetEmployeeDetailAsync(teacherId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("{teacherId:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryEmployees.Edit)]
    [ProducesResponseType(typeof(EmployeeSalaryDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignOrUpdate(
        Guid teacherId,
        [FromBody] AssignEmployeeSalaryRequestDto? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var result = await _service.AssignOrUpdateAsync(teacherId, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
