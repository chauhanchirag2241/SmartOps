using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Fees.Controllers;

[ApiController]
[Route("api/fees/structure")]
[Authorize]
public sealed class FeeStructureController : ControllerBase
{
    private readonly IFeeStructureService _service;

    public FeeStructureController(IFeeStructureService service) => _service = service;

    [HttpGet("types")]
    [Authorize(Policy = MenuPolicies.FeesStructure.View)]
    [ProducesResponseType(typeof(IList<FeeTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeeTypes(CancellationToken ct)
    {
        var result = await _service.GetFeeTypesAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("stats")]
    [Authorize(Policy = MenuPolicies.FeesStructure.View)]
    [ProducesResponseType(typeof(FeeStructureStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _service.GetStatsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("settings")]
    [Authorize(Policy = MenuPolicies.FeesStructure.View)]
    [ProducesResponseType(typeof(FeeSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var result = await _service.GetSettingsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("settings")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertSettings([FromBody] UpsertFeeSettingsRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpsertSettingsAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("types")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Add)]
    [ProducesResponseType(typeof(FeeTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFeeType([FromBody] CreateFeeTypeRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateFeeTypeAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("types/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFeeType(Guid id, [FromBody] UpdateFeeTypeRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateFeeTypeAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("types/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFeeType(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteFeeTypeAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPatch("types/{id:guid}/active")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] ToggleFeeTypeActiveRequestDto request, CancellationToken ct)
    {
        var result = await _service.SetActiveAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
