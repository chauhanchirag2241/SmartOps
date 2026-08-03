using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Leave.Controllers;

[ApiController]
[Route("api/leave/policies")]
[Authorize]
public sealed class LeavePoliciesController : ControllerBase
{
    private readonly ILeavePolicyService _service;

    public LeavePoliciesController(ILeavePolicyService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.LeavePolicies.View)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut]
    [Authorize(Policy = MenuPolicies.LeavePolicies.Edit)]
    public async Task<IActionResult> Upsert([FromBody] UpsertLeavePolicyDto request, CancellationToken ct)
    {
        var result = await _service.UpsertAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeavePolicies.Edit)]
    public async Task<IActionResult> UpdateMonthly(Guid id, [FromBody] UpdateLeavePolicyMonthlyDto request, CancellationToken ct)
    {
        var result = await _service.UpdateMonthlyAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeavePolicies.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
