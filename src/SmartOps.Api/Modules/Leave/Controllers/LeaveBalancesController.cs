using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Leave.Controllers;

[ApiController]
[Route("api/leave/balances")]
[Authorize]
public sealed class LeaveBalancesController : ControllerBase
{
    private readonly ILeaveBalanceService _service;

    public LeaveBalancesController(ILeaveBalanceService service) => _service = service;

    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = MenuPolicies.LeaveBalances.View)]
    public async Task<IActionResult> GetByEmployee(
        Guid employeeId,
        [FromQuery] Guid? academicYearId,
        CancellationToken ct)
    {
        var result = await _service.GetByEmployeeAsync(employeeId, academicYearId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("mine")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await _service.GetMineAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("employee/{employeeId:guid}/ledger")]
    [Authorize(Policy = MenuPolicies.LeaveBalances.View)]
    public async Task<IActionResult> GetLedger(
        Guid employeeId,
        [FromQuery] Guid? leaveTypeId,
        CancellationToken ct)
    {
        var result = await _service.GetLedgerAsync(employeeId, leaveTypeId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("credit/manual")]
    [Authorize(Policy = MenuPolicies.LeaveBalances.Edit)]
    public async Task<IActionResult> ManualCredit([FromBody] ManualCreditLeaveDto request, CancellationToken ct)
    {
        var result = await _service.ManualCreditAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
