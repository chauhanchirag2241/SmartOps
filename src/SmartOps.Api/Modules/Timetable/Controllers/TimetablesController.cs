using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Timetable;
using SmartOps.Application.Modules.Timetable.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Timetable.Controllers;

[ApiController]
[Route("api/timetables")]
[Authorize]
public sealed class TimetablesController(ITimetableService timetableService) : ControllerBase
{
    [HttpGet("versions")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TimetableVersionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(
        [FromQuery] Guid classId,
        [FromQuery] Guid academicYearId,
        CancellationToken ct)
    {
        var result = await timetableService.GetVersionsAsync(classId, academicYearId, ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("versions")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.Add)]
    [ProducesResponseType(typeof(CreateTimetableResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateVersion([FromBody] CreateTimetableVersionDto request, CancellationToken ct)
    {
        try
        {
            var result = await timetableService.CreateVersionAsync(request, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{timetableId:guid}/grid")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.View)]
    [ProducesResponseType(typeof(TimetableGridDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrid(Guid timetableId, CancellationToken ct)
    {
        try
        {
            var result = await timetableService.GetGridAsync(timetableId, ct).ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
        }
    }

    [HttpGet("class-grid")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.View)]
    [ProducesResponseType(typeof(TimetableGridDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassGrid(
        [FromQuery] Guid classId,
        [FromQuery] Guid academicYearId,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await timetableService.GetClassGridAsOfAsync(classId, academicYearId, date, ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("teacher-grid")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.View)]
    [ProducesResponseType(typeof(TimetableGridDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherGrid(
        [FromQuery] Guid employeeId,
        [FromQuery] Guid academicYearId,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await timetableService.GetTeacherGridAsync(employeeId, academicYearId, date, ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPut("{timetableId:guid}/slots")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveSlots(Guid timetableId, [FromBody] UpsertTimetableSlotsDto request, CancellationToken ct)
    {
        try
        {
            await timetableService.SaveSlotsAsync(timetableId, request, ct).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("validate-conflicts")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.View)]
    [ProducesResponseType(typeof(ConflictCheckResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateConflicts([FromBody] ValidateConflictsDto request, CancellationToken ct)
    {
        var result = await timetableService.ValidateConflictsAsync(request, ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpDelete("{timetableId:guid}")]
    [Authorize(Policy = MenuPolicies.ClassTimetable.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteVersion(Guid timetableId, CancellationToken ct)
    {
        await timetableService.DeleteVersionAsync(timetableId, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("my")]
    [Authorize(Policy = MenuPolicies.MyTimetable.View)]
    [ProducesResponseType(typeof(MyTimetableResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTimetable(
        [FromQuery] Guid academicYearId,
        [FromQuery] DateOnly? asOf,
        CancellationToken ct)
    {
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await timetableService.GetMyTimetableAsync(academicYearId, date, ct).ConfigureAwait(false);
        return Ok(result);
    }
}
