using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.FrontOffice.Controllers;

[ApiController]
[Route("api/front-office/complaint-types")]
[Authorize]
public sealed class ComplaintTypesController : ControllerBase
{
    private readonly IFrontOfficeService _service;

    public ComplaintTypesController(IFrontOfficeService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.FrontOfficeSetup.View)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? activeFilter = "All",
        CancellationToken ct = default)
    {
        var result = await _service.GetComplaintTypesAsync(activeFilter, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FrontOfficeSetup.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.FrontOfficeSetup.Add)]
    public async Task<IActionResult> Create([FromBody] CreateComplaintTypeRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateComplaintTypeAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FrontOfficeSetup.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateComplaintTypeRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateComplaintTypeAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.FrontOfficeSetup.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteComplaintTypeAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
