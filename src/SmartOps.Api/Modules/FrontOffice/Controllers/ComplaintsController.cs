using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.FrontOffice.Controllers;

[ApiController]
[Route("api/front-office/complaints")]
[Authorize]
public sealed class ComplaintsController : ControllerBase
{
    private readonly IFrontOfficeService _service;
    private readonly IAuditLogRepository _auditLogRepository;

    public ComplaintsController(IFrontOfficeService service, IAuditLogRepository auditLogRepository)
    {
        _service = service;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Complaints.View)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? activeFilter = "All",
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int? status = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetComplaintsAsync(activeFilter, fromDate, toDate, status, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Complaints.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Complaints.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableComplaints, id, page, pageSize, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Complaints.Add)]
    public async Task<IActionResult> Create([FromBody] CreateComplaintRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateComplaintAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Complaints.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateComplaintRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateComplaintAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Complaints.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteComplaintAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
