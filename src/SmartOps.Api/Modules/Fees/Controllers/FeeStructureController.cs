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
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _service.GetVersionsAsync(status, ct).ConfigureAwait(false);
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

    [HttpPost("versions/{versionId:guid}/heads")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Add)]
    [ProducesResponseType(typeof(FeeHeadDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFeeHead(Guid versionId, [FromBody] CreateFeeHeadRequestDto request, CancellationToken ct)
    {
        var body = request with { FeeStructureId = versionId };
        var result = await _service.CreateFeeHeadAsync(body, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("heads/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Edit)]
    [ProducesResponseType(typeof(FeeHeadDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFeeHead(Guid id, [FromBody] UpdateFeeHeadRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateFeeHeadAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("heads/{id:guid}")]
    [Authorize(Policy = MenuPolicies.FeesStructure.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteFeeHead(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteFeeHeadAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
