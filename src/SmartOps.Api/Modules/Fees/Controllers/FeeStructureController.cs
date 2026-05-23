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

    [HttpGet("versions")]
    [Authorize(Policy = MenuPolicies.FeesStructure.View)]
    [ProducesResponseType(typeof(IList<FeeStructureVersionListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(
        [FromQuery] Guid? academicYearId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _service.GetVersionsAsync(academicYearId, status, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("versions/{versionId:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.View)]
    [ProducesResponseType(typeof(FeeStructureVersionDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersionDetail(Guid versionId, CancellationToken ct)
    {
        var result = await _service.GetVersionDetailAsync(versionId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("versions")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Add)]
    [ProducesResponseType(typeof(FeeStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateVersion([FromBody] CreateFeeStructureVersionRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateVersionAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.PublishVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/activate")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.ActivateVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/new-version")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Add)]
    [ProducesResponseType(typeof(FeeStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateNewVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.CreateNewVersionFromAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("versions/{versionId:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.DeleteVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/types")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Add)]
    [ProducesResponseType(typeof(FeeTypeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFeeType(Guid versionId, [FromBody] CreateFeeTypeRequestDto request, CancellationToken ct)
    {
        var body = request with { FeeStructureVersionId = versionId };
        var result = await _service.CreateFeeTypeAsync(body, ct).ConfigureAwait(false);
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
}
