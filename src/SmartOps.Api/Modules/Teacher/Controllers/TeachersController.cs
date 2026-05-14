using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Models;

namespace SmartOps.Api.Modules.Teacher.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TeachersController(ITeacherRepository teacherRepository) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous] // For demo purposes, matching StudentsController reference if applicable
    [ProducesResponseType(typeof(CreateTeacherResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateTeacherResponse>> CreateTeacher(
        [FromBody] CreateTeacherDto request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest("Teacher data is required.");

        var entity = request.ToEntity();
        var teacherId = await teacherRepository.CreateTeacherAsync(entity, cancellationToken).ConfigureAwait(false);

        return Ok(new CreateTeacherResponse("Teacher created successfully", teacherId));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllTeachers(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var result = await teacherRepository
            .GetAllTeachersAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeacherEntity>> GetTeacherById(Guid id, CancellationToken cancellationToken)
    {
        var teacher = await teacherRepository.GetTeacherByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return teacher is null ? NotFound() : Ok(teacher);
    }

    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateTeacher(Guid id, [FromBody] TeacherEntity teacher, CancellationToken cancellationToken)
    {
        if (id != teacher.Id) return BadRequest("Route id and payload id must match.");

        await teacherRepository.UpdateTeacherAsync(teacher, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteTeacher(Guid id, CancellationToken cancellationToken)
    {
        await teacherRepository.DeleteTeacherAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
