using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Teacher.Controllers;

[ApiController]
[Route("api/mappings")]
[Authorize]
public sealed class ClassSubjectTeacherMappingsController(
    IClassSubjectTeacherMappingService mappingService) : ControllerBase
{
    [HttpGet("by-employee/{employeeId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSubjectTeacherMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSubjectTeacherMappingDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await mappingService
            .GetByEmployeeAsync(employeeId, academicYearId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpGet("by-class/{classId:guid}")]
    [HttpGet("by-class-group/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSubjectTeacherMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSubjectTeacherMappingDto>>> GetByClass(
        Guid classId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        // Parameter is class group id (route names retained for compatibility).
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await mappingService
            .GetByClassAsync(classId, academicYearId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Create(
        [FromBody] CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Mapping data is required.");
        }

        try
        {
            ClassSubjectTeacherMappingDto created = await mappingService
                .AddMappingAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("bulk")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(BulkCreateClassSubjectTeacherMappingsResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkCreateClassSubjectTeacherMappingsResultDto>> BulkCreate(
        [FromBody] BulkCreateClassSubjectTeacherMappingsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Mapping data is required.");
        }

        try
        {
            BulkCreateClassSubjectTeacherMappingsResultDto result = await mappingService
                .BulkAddMappingsAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Update(
        Guid id,
        [FromBody] UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Mapping data is required.");
        }

        try
        {
            ClassSubjectTeacherMappingDto updated = await mappingService
                .UpdateMappingAsync(id, request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mappingService.DeleteMappingAsync(id, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
