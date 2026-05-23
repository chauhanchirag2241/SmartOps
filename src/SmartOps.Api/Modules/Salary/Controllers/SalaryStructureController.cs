using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Salary;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Salary.Controllers;

[ApiController]
[Route("api/salary/structure")]
[Authorize]
public sealed class SalaryStructureController : ControllerBase
{
    private readonly ISalaryStructureService _service;

    public SalaryStructureController(ISalaryStructureService service) => _service = service;

    [HttpGet("versions")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.View)]
    [ProducesResponseType(typeof(IList<SalaryStructureVersionListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(
        [FromQuery] Guid? academicYearId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var result = await _service.GetVersionsAsync(academicYearId, status, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("versions/{versionId:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.View)]
    [ProducesResponseType(typeof(SalaryStructureVersionDetailDto), StatusCodes.Status200OK)]
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
    [Authorize(Policy = MenuPolicies.SalaryStructure.Add)]
    [ProducesResponseType(typeof(SalaryStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateVersion([FromBody] CreateSalaryStructureVersionRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateVersionAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Edit)]
    [ProducesResponseType(typeof(SalaryStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.PublishVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/activate")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Edit)]
    [ProducesResponseType(typeof(SalaryStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.ActivateVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/new-version")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Add)]
    [ProducesResponseType(typeof(SalaryStructureVersionListItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateNewVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.CreateNewVersionFromAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("versions/{versionId:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteVersion(Guid versionId, CancellationToken ct)
    {
        var result = await _service.DeleteVersionAsync(versionId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("versions/{versionId:guid}/components")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Add)]
    [ProducesResponseType(typeof(SalaryVersionComponentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateComponent(Guid versionId, [FromBody] CreateSalaryVersionComponentRequestDto request, CancellationToken ct)
    {
        var body = request with { SalaryStructureVersionId = versionId };
        var result = await _service.CreateComponentAsync(body, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("components/{id:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Edit)]
    [ProducesResponseType(typeof(SalaryVersionComponentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateComponent(Guid id, [FromBody] UpdateSalaryVersionComponentRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateComponentAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("components/{id:guid}")]
    [Authorize(Policy = MenuPolicies.SalaryStructure.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteComponent(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteComponentAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
