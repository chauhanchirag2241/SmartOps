using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Student.DTOs;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Interfaces;
using SmartOps.Domain.Modules.Student.Models;

namespace SmartOps.Api.Modules.Student.Controllers;

/// <summary>
/// Student CRUD + list. Pattern: thin controller → <see cref="IStudentRepository"/> (copy for other modules).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StudentsController(IStudentRepository studentRepository) : ControllerBase
{
    /// <summary>Create a student and related rows (parents, academics, etc.).</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateStudentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateStudentResponse>> CreateStudent(
        [FromBody] CreateStudentDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Student data is required.");
        }

        var entity = request.ToEntity();
        var studentId = await studentRepository.CreateStudentAsync(entity, cancellationToken).ConfigureAwait(false);

        return Ok(new CreateStudentResponse("Student created successfully", studentId));
    }

    /// <summary>Paged list with optional search, sort, and status filter.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<StudentListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStudents(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] StudentFilter filter = StudentFilter.Active,
        [FromQuery] Guid? classId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await studentRepository
            .GetAllStudentsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, classId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>Full student graph by id (active only).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StudentEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentEntity>> GetStudentById(Guid id, CancellationToken cancellationToken)
    {
        var student = await studentRepository.GetStudentByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return student is null ? NotFound() : Ok(student);
    }

    /// <summary>Replace student aggregate (body <see cref="StudentEntity.Id"/> must match route).</summary>
    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] StudentEntity student, CancellationToken cancellationToken)
    {
        if (id != student.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        await studentRepository.UpdateStudentAsync(student, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Soft-delete student and related rows.</summary>
    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteStudent(Guid id, CancellationToken cancellationToken)
    {
        await studentRepository.DeleteStudentAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
