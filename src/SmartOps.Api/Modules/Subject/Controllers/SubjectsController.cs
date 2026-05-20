using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Subject;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Subject.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SubjectsController(ISubjectRepository subjectRepository) : ControllerBase
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
    public async Task<IActionResult> UpdateSubject(Guid id, [FromBody] SubjectEntity subject, CancellationToken ct)
    {
        if (id != subject.Id) return BadRequest("ID mismatch");
        await subjectRepository.UpdateSubjectAsync(subject, ct).ConfigureAwait(false);
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
}
