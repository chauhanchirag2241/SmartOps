using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Timetable;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Timetable;

namespace SmartOps.Api.Modules.Timetable.Controllers;

[ApiController]
[Route("api/period-templates")]
[Authorize]
public sealed class PeriodTemplatesController(
    IPeriodTemplateRepository templateRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Add)]
    [ProducesResponseType(typeof(CreatePeriodTemplateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatePeriodTemplateResponse>> Create(
        [FromBody] CreatePeriodTemplateDto request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Template name is required.");
        if (request.Periods is null || request.Periods.Count == 0)
            return BadRequest("Add at least one period to the template.");

        var entity = request.ToEntity();
        entity.Id = Guid.NewGuid();
        var periods = request.ToPeriodEntities(entity.Id);

        var id = await templateRepository.CreateTemplateAsync(entity, periods, ct).ConfigureAwait(false);
        return Ok(new CreatePeriodTemplateResponse("Period template created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(PagedResult<PeriodTemplateListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var result = await templateRepository
            .GetAllTemplatesAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("/api/period-template/dropdown")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDropdown(CancellationToken ct)
    {
        var result = await templateRepository.GetTemplateDropdownAsync(ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(PeriodTemplateDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PeriodTemplateDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var template = await templateRepository.GetTemplateByIdAsync(id, ct, includeInactive: true).ConfigureAwait(false);
        if (template is null) return NotFound();

        var periods = await templateRepository.GetPeriodsByTemplateIdAsync(id, ct).ConfigureAwait(false);
        return Ok(new PeriodTemplateDetailDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsActive = template.IsActive,
            Periods = periods.Select(p => new PeriodLineDto
            {
                Id = p.Id,
                Name = p.Name,
                ShortName = p.ShortName,
                PeriodOrder = p.PeriodOrder,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                IsBreak = p.IsBreak,
            }).ToList(),
        });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePeriodTemplateDto request, CancellationToken ct)
    {
        if (request is null) return BadRequest("Template data is required.");
        if (request.Periods is null || request.Periods.Count == 0)
            return BadRequest("Add at least one period to the template.");

        var existing = await templateRepository.GetTemplateByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null) return NotFound();

        var entity = request.ToEntity();
        entity.Id = id;
        entity.BranchId = existing.BranchId;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;

        var periods = request.ToPeriodEntities(id);
        await templateRepository.UpdateTemplateAsync(entity, periods, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await templateRepository.DeleteTemplateAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TablePeriodTemplates, id, page, pageSize, ct);
        return Ok(result);
    }
}
