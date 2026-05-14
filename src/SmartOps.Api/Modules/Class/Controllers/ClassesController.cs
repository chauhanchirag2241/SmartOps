using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Class.DTOs;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class.Interfaces;
using SmartOps.Domain.Modules.Class.Models;

namespace SmartOps.Api.Modules.Class.Controllers;

/// <summary>
/// Class CRUD + list. Pattern: thin controller → <see cref="IClassRepository"/> (same as StudentsController).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ClassesController(IClassRepository classRepository) : ControllerBase
{
    /// <summary>Create a class section record.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateClassResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateClassResponse>> CreateClass(
        [FromBody] CreateClassDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Class data is required.");
        }

        var entity = request.ToEntity();
        var classId = await classRepository.CreateClassAsync(entity, cancellationToken).ConfigureAwait(false);

        return Ok(new CreateClassResponse("Class created successfully", classId));
    }

    /// <summary>Paged list with optional search, sort, and status filter.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ClassListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllClasses(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var result = await classRepository
            .GetAllClassesAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>Active class dropdown data without pagination.</summary>
    [HttpGet("/api/class/dropdown")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassDropdown(CancellationToken cancellationToken)
    {
        var result = await classRepository.GetClassDropdownAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Full class record by id (active only).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClassEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassEntity>> GetClassById(Guid id, CancellationToken cancellationToken)
    {
        var classEntity = await classRepository.GetClassByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return classEntity is null ? NotFound() : Ok(classEntity);
    }

    /// <summary>Replace class record (body <see cref="ClassEntity.Id"/> must match route).</summary>
    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateClass(Guid id, [FromBody] ClassEntity classEntity, CancellationToken cancellationToken)
    {
        if (id != classEntity.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        await classRepository.UpdateClassAsync(classEntity, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Soft-delete class record.</summary>
    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken cancellationToken)
    {
        await classRepository.DeleteClassAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
