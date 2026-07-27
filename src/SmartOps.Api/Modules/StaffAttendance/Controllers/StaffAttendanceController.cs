using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.StaffAttendance.Controllers;

[ApiController]
[Route("api/staff-attendance")]
[Authorize]
public sealed class StaffAttendanceController : ControllerBase
{
    private readonly IStaffAttendanceService _service;
    private readonly ILogger<StaffAttendanceController> _logger;

    public StaffAttendanceController(
        IStaffAttendanceService service,
        ILogger<StaffAttendanceController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("settings")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.View)]
    [ProducesResponseType(typeof(EmployeeAttendanceTypeSettingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var result = await _service.GetSettingsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.StaffAttendance.View)]
    [ProducesResponseType(typeof(IList<StaffAttendanceRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByDate([FromQuery] DateOnly? date, CancellationToken ct)
    {
        DateOnly attendanceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await _service.ListByDateAsync(attendanceDate, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("my-today")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.View)]
    [ProducesResponseType(typeof(StaffAttendanceRowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyToday(CancellationToken ct)
    {
        var result = await _service.GetMyTodayAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("manual")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.Edit)]
    [ProducesResponseType(typeof(StaffAttendanceRowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ManualPunch([FromBody] ManualPunchRequestDto request, CancellationToken ct)
    {
        var result = await _service.ManualPunchAsync(request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Manual staff punch failed: {Error}", result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.Edit)]
    [ProducesResponseType(typeof(StaffAttendanceRowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStaffAttendanceRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("face/enroll")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.Edit)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> EnrollFace(
        [FromQuery] Guid? employeeId,
        IFormFile image,
        CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest("Image is required.");
        }

        await using var stream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        byte[] bytes = ms.ToArray();

        var result = await _service
            .EnrollFaceAsync(employeeId, bytes, image.ContentType, image.FileName, ct)
            .ConfigureAwait(false);

        return result.IsSuccess ? Ok(new { Message = "Face enrolled successfully." }) : BadRequest(result.Error);
    }

    [HttpPost("face/punch")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.Edit)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StaffAttendanceRowDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> FacePunch(IFormFile image, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest("Image is required.");
        }

        await using var stream = image.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        byte[] bytes = ms.ToArray();

        var result = await _service.FacePunchAsync(bytes, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Face punch failed: {Error}", result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("face/enroll/{employeeId:guid}")]
    [Authorize(Policy = MenuPolicies.StaffAttendance.Edit)]
    public async Task<IActionResult> DeactivateFaceEnrollment(Guid employeeId, CancellationToken ct)
    {
        var result = await _service.DeactivateFaceEnrollmentAsync(employeeId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpGet("report")]
    [Authorize(Policy = MenuPolicies.StaffAttendanceReport.View)]
    [ProducesResponseType(typeof(StaffAttendanceReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] int month,
        [FromQuery] int year,
        [FromQuery] Guid? departmentId,
        CancellationToken ct)
    {
        var result = await _service.GetReportAsync(month, year, departmentId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
