using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Salary.Controllers;

[ApiController]
[Route("api/salary/payroll")]
[Authorize]
public sealed class PayrollController : ControllerBase
{
    private readonly IPayrollService _service;

    public PayrollController(IPayrollService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.SalaryPayroll.View)]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayroll(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        var result = await _service.GetPayrollAsync(year, month, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("process")]
    [Authorize(Policy = MenuPolicies.SalaryPayroll.Add)]
    [ProducesResponseType(typeof(PayrollRunDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPayroll([FromBody] ProcessPayrollRequestDto request, CancellationToken ct)
    {
        var result = await _service.ProcessPayrollAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{runId:guid}/mark-paid")]
    [Authorize(Policy = MenuPolicies.SalaryPayroll.Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkPaid(Guid runId, [FromBody] MarkPayrollPaidRequestDto request, CancellationToken ct)
    {
        var result = await _service.MarkPaidAsync(runId, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("entries/{entryId:guid}/payslip")]
    [Authorize(Policy = MenuPolicies.SalaryPayroll.View)]
    [ProducesResponseType(typeof(PayslipDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayslip(Guid entryId, CancellationToken ct)
    {
        var result = await _service.GetPayslipAsync(entryId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}
