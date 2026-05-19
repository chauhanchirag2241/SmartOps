using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Class.DTOs;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class.Interfaces;
using SmartOps.Domain.Modules.Class.Models;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Class.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ClassesController(IClassRepository classRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Classes.Add)]
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

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Classes.View)]
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

    [HttpGet("/api/class/dropdown")]
    [Authorize(Policy = MenuPolicies.Classes.ListForAttendanceDropdown)]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassDropdown(
        [FromQuery] bool forAttendance = false,
        CancellationToken cancellationToken = default)
    {
        var result = await classRepository.GetClassDropdownAsync(forAttendance, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.View)]
    [ProducesResponseType(typeof(ClassEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassEntity>> GetClassById(Guid id, CancellationToken cancellationToken)
    {
        var classEntity = await classRepository.GetClassByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return classEntity is null ? NotFound() : Ok(classEntity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.Edit)]
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

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Classes.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteClass(Guid id, CancellationToken cancellationToken)
    {
        await classRepository.DeleteClassAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
