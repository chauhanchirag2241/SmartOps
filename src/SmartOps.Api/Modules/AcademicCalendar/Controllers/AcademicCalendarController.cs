using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.AcademicCalendar;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.AcademicCalendar;

namespace SmartOps.Api.Modules.AcademicCalendar.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AcademicCalendarController(IAcademicCalendarService calendarService) : ControllerBase
{
    // ── Event types ──────────────────────────────────────────────

    [HttpGet("event-types")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.View)]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarEventTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventTypes(CancellationToken ct)
    {
        var result = await calendarService.GetEventTypesAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("event-types")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Add)]
    [ProducesResponseType(typeof(CreateCalendarEventTypeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEventType([FromBody] CreateCalendarEventTypeDto dto, CancellationToken ct)
    {
        var result = await calendarService.CreateEventTypeAsync(dto, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("event-types/{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEventType(Guid id, [FromBody] UpdateCalendarEventTypeDto dto, CancellationToken ct)
    {
        var result = await calendarService.UpdateEventTypeAsync(id, dto, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("event-types/{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEventType(Guid id, CancellationToken ct)
    {
        var result = await calendarService.DeleteEventTypeAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    // ── Weekend settings ─────────────────────────────────────────

    [HttpGet("weekend-settings")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.View)]
    [ProducesResponseType(typeof(CalendarWeekendSettingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeekendSettings(CancellationToken ct)
    {
        var result = await calendarService.GetWeekendSettingsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("weekend-settings")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Edit)]
    [ProducesResponseType(typeof(CalendarWeekendSettingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertWeekendSettings(
        [FromBody] UpsertCalendarWeekendSettingDto dto,
        CancellationToken ct)
    {
        var result = await calendarService.UpsertWeekendSettingsAsync(dto, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    // ── Events ───────────────────────────────────────────────────

    [HttpGet("events")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.View)]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? academicYearId = null,
        CancellationToken ct = default)
    {
        var result = await calendarService.GetEventsForRangeAsync(academicYearId, from, to, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("events/{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.View)]
    [ProducesResponseType(typeof(CalendarEventDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken ct)
    {
        var result = await calendarService.GetEventByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("events")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Add)]
    [ProducesResponseType(typeof(CreateCalendarEventResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateCalendarEventDto dto, CancellationToken ct)
    {
        var result = await calendarService.CreateEventAsync(dto, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("events/{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateCalendarEventDto dto, CancellationToken ct)
    {
        var result = await calendarService.UpdateEventAsync(id, dto, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken ct)
    {
        var result = await calendarService.DeleteEventAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    // ── Working days ─────────────────────────────────────────────

    [HttpGet("working-days")]
    [Authorize(Policy = MenuPolicies.AcademicCalendar.View)]
    [ProducesResponseType(typeof(WorkingDaysResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkingDays(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] CalendarAudience audience = CalendarAudience.Students,
        CancellationToken ct = default)
    {
        var result = await calendarService.GetWorkingDaysAsync(year, month, audience, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
