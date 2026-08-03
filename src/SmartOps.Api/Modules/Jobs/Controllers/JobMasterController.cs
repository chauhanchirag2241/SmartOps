using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Jobs;
using SmartOps.Application.Modules.Jobs.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Jobs.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize]
public sealed class JobMasterController : ControllerBase
{
    private readonly IJobMasterService _service;

    public JobMasterController(IJobMasterService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.JobMaster.View)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.JobMaster.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateJobDefinitionDto request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("hangfire")]
    [Authorize(Policy = MenuPolicies.JobMaster.View)]
    public async Task<IActionResult> GetHangfireStatus(CancellationToken ct)
    {
        var result = await _service.GetHangfireStatusAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("hangfire")]
    [Authorize(Policy = MenuPolicies.JobMaster.Edit)]
    public async Task<IActionResult> SetHangfireStatus([FromBody] HangfireStatusDto request, CancellationToken ct)
    {
        var result = await _service.SetHangfireEnabledAsync(request.IsEnabled, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
