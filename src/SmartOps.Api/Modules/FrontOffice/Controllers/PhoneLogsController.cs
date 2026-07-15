using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.FrontOffice.Controllers;

[ApiController]
[Route("api/front-office/phone-logs")]
[Authorize]
public sealed class PhoneLogsController : ControllerBase
{
    private readonly IFrontOfficeService _service;
    private readonly IAuditLogRepository _auditLogRepository;

    public PhoneLogsController(IFrontOfficeService service, IAuditLogRepository auditLogRepository)
    {
        _service = service;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.PhoneLogs.View)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? activeFilter = "All",
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetPhoneLogsAsync(activeFilter, fromDate, toDate, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PhoneLogs.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.PhoneLogs.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TablePhoneLogs, id, page, pageSize, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.PhoneLogs.Add)]
    public async Task<IActionResult> Create([FromBody] CreatePhoneLogRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreatePhoneLogAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PhoneLogs.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePhoneLogRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdatePhoneLogAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PhoneLogs.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeletePhoneLogAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
