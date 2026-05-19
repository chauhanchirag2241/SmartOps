using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Teacher.Controllers;

[ApiController]
[Route("api/mappings")]
[Authorize]
public sealed class ClassSubjectTeacherMappingsController(
    IClassSubjectTeacherMappingService mappingService) : ControllerBase
{
    [HttpGet("by-class/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.View)]
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

    [HttpGet("by-teacher/{teacherId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.View)]
    [ProducesResponseType(typeof(TeacherAssignmentsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherAssignmentsResponseDto>> GetByTeacher(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        TeacherAssignmentsResponseDto result = await mappingService
            .GetTeacherAssignmentsAsync(teacherId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("by-subject/{subjectId:guid}")]
    [Authorize(Policy = MenuPolicies.Subjects.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ClassSubjectTeacherMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassSubjectTeacherMappingDto>>> GetBySubject(
        Guid subjectId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await mappingService
            .GetBySubjectIdAsync(subjectId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Create(
        [FromBody] CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingDto created = await mappingService
            .AddMappingAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return CreatedAtAction(nameof(GetByClass), new { classId = created.ClassId }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(typeof(ClassSubjectTeacherMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassSubjectTeacherMappingDto>> Update(
        Guid id,
        [FromBody] UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingDto updated = await mappingService
            .UpdateMappingAsync(id, request, cancellationToken)
            .ConfigureAwait(false);

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mappingService.RemoveMappingAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPut("by-class/{classId:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveClassMappings(
        Guid classId,
        [FromBody] SaveClassMappingsRequestDto request,
        CancellationToken cancellationToken)
    {
        await mappingService.SaveClassMappingsAsync(classId, request, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPut("by-teacher/{teacherId:guid}")]
    [Authorize(Policy = MenuPolicies.Teachers.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveTeacherMappings(
        Guid teacherId,
        [FromBody] SaveTeacherAssignmentsRequestDto request,
        CancellationToken cancellationToken)
    {
        await mappingService.SaveTeacherAssignmentsAsync(teacherId, request, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPut("by-subject/{subjectId:guid}")]
    [Authorize(Policy = MenuPolicies.Subjects.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveSubjectMappings(
        Guid subjectId,
        [FromBody] SaveSubjectMappingsRequestDto request,
        CancellationToken cancellationToken)
    {
        await mappingService.SaveSubjectMappingsAsync(subjectId, request, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
