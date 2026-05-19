using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Attendance.DTOs;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Attendance;

[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;
    private readonly IResourceAuthorizationService _resourceAuthorization;
    private readonly IValidator<SubmitAttendanceRequestDto> _validator;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService service,
        IResourceAuthorizationService resourceAuthorization,
        IValidator<SubmitAttendanceRequestDto> validator,
        ILogger<AttendanceController> logger)
    {
        _service = service;
        _resourceAuthorization = resourceAuthorization;
        _validator = validator;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Attendance.View)]
    [ProducesResponseType(typeof(ClassAttendanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClassAttendance(
        [FromQuery] Guid classId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        if (!await _resourceAuthorization.CanMarkAttendanceForClassAsync(classId, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result =
            await _service.GetClassAttendanceAsync(
                new GetClassAttendanceRequestDto(classId, date), ct)
            .ConfigureAwait(false);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("submit")]
    [Authorize(Policy = MenuPolicies.Attendance.Edit)]
    [ProducesResponseType(typeof(ClassAttendanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitAttendance(
        [FromBody] SubmitAttendanceRequestDto request,
        CancellationToken ct)
    {
        if (!await _resourceAuthorization.CanMarkAttendanceForClassAsync(request.ClassId, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        var validation = await _validator.ValidateAsync(request, ct).ConfigureAwait(false);

        //if (!validation.IsValid)
        //{
        //    return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        //}

        var result =
            await _service.SubmitAttendanceAsync(request, ct)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Attendance submission failed for class {ClassId}: {Error}", request.ClassId, result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("student/{studentId:guid}/summary")]
    [Authorize(Policy = MenuPolicies.Attendance.View)]
    [ProducesResponseType(typeof(StudentAttendanceSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStudentSummary(
        Guid studentId,
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken ct)
    {
        if (!await _resourceAuthorization.CanAccessStudentAsync(studentId, AccessLevel.View, ct).ConfigureAwait(false))
        {
            return NotFound();
        }

        var result =
            await _service.GetStudentSummaryAsync(studentId, month, year, ct)
            .ConfigureAwait(false);

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
