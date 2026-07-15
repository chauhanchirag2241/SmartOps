using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.FrontOffice.Controllers;

[ApiController]
[Route("api/front-office/visitors")]
[Authorize]
public sealed class VisitorsController : ControllerBase
{
    private readonly IFrontOfficeService _service;
    private readonly IAuditLogRepository _auditLogRepository;

    public VisitorsController(IFrontOfficeService service, IAuditLogRepository auditLogRepository)
    {
        _service = service;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.VisitorBook.View)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? activeFilter,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var result = await _service.GetVisitorsAsync(activeFilter ?? "All", fromDate, toDate, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.VisitorBook.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.VisitorBook.View)]
    public async Task<IActionResult> GetHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _auditLogRepository
            .GetEntityHistoryAsync(DatabaseConfig.TableVisitors, id, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.VisitorBook.Add)]
    public async Task<IActionResult> Create([FromBody] CreateVisitorRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateVisitorAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.VisitorBook.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVisitorRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateVisitorAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/checkout")]
    [Authorize(Policy = MenuPolicies.VisitorBook.Edit)]
    public async Task<IActionResult> Checkout(Guid id, CancellationToken ct)
    {
        var result = await _service.CheckoutVisitorAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.VisitorBook.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteVisitorAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
