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
    [HttpGet("lookups")]
    [Authorize(Policy = MenuPolicies.ClassMappings.View)]
    [ProducesResponseType(typeof(MappingLookupsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MappingLookupsResponseDto>> GetLookups(
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        try
        {
            MappingLookupsResponseDto result = await mappingService
                .GetLookupsAsync(academicYearId, cancellationToken)
                .ConfigureAwait(false);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("by-class/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.ClassMappings.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSubjectTeacherMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSubjectTeacherMappingDto>>> GetByClass(
        Guid classId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await mappingService
            .GetByClassIdAsync(classId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.ClassMappings.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Create(
        [FromBody] CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            ClassSubjectTeacherMappingDto created = await mappingService
                .AddMappingAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return CreatedAtAction(nameof(GetByClass), new { classId = created.ClassId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ClassMappings.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Update(
        Guid id,
        [FromBody] UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            ClassSubjectTeacherMappingDto updated = await mappingService
                .UpdateMappingAsync(id, request, cancellationToken)
                .ConfigureAwait(false);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/class-teacher")]
    [Authorize(Policy = MenuPolicies.ClassMappings.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> SetClassTeacher(
        Guid id,
        [FromBody] SetClassTeacherRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            ClassSubjectTeacherMappingDto updated = await mappingService
                .SetClassTeacherAsync(id, request.IsClassTeacher, cancellationToken)
                .ConfigureAwait(false);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/assign-teacher")]
    [Authorize(Policy = MenuPolicies.ClassMappings.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> AssignTeacherLater(
        Guid id,
        [FromBody] AssignTeacherLaterRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            ClassSubjectTeacherMappingDto updated = await mappingService
                .AssignTeacherLaterAsync(id, request, cancellationToken)
                .ConfigureAwait(false);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ClassMappings.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mappingService.RemoveMappingAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
