using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Subject;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Subject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SubjectsController(
    ISubjectRepository subjectRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Subjects.Add)]
    [ProducesResponseType(typeof(CreateSubjectResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateSubjectResponse>> CreateSubject([FromBody] CreateSubjectDto request, CancellationToken ct)
    {
        var entity = request.ToEntity();
        var id = await subjectRepository.CreateSubjectAsync(entity, ct).ConfigureAwait(false);
        return Ok(new CreateSubjectResponse("Subject created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Subjects.View)]
    [ProducesResponseType(typeof(PagedResult<SubjectListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSubjects(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var result = await subjectRepository
            .GetAllSubjectsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("/api/subject/dropdown")]
    [Authorize(Policy = MenuPolicies.Subjects.View)]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjectDropdown(CancellationToken ct)
    {
        var result = await subjectRepository.GetSubjectDropdownAsync(ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Subjects.View)]
    [ProducesResponseType(typeof(SubjectEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectEntity>> GetSubjectById(Guid id, CancellationToken ct)
    {
        var subject = await subjectRepository.GetSubjectByIdAsync(id, ct).ConfigureAwait(false);
        return subject is null ? NotFound() : Ok(subject);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Subjects.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] CreateSubjectDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Subject data is required.");
        }

        var existing = await subjectRepository.GetSubjectByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var entity = request.ToEntity();
        entity.Id = id;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;

        if (request.AssignedClasses is null or { Length: 0 })
        {
            entity.AssignedClasses = existing.AssignedClasses;
        }

        if (request.TeachingDays is null or { Length: 0 })
        {
            entity.TeachingDays = existing.TeachingDays;
        }

        await subjectRepository.UpdateSubjectAsync(entity, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Subjects.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSubject(Guid id, CancellationToken ct)
    {
        await subjectRepository.DeleteSubjectAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Subjects.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableSubjects, id, page, pageSize, ct);

        return Ok(result);
    }
}
