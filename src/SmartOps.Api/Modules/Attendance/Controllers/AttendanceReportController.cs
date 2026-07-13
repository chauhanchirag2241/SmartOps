using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Attendance;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Attendance.Controllers;

[ApiController]
[Route("api/attendance/report")]
[Authorize(Policy = MenuPolicies.Attendance.View)]
public sealed class AttendanceReportController : ControllerBase
{
    private readonly IAttendanceReportRepository _repository;
    private readonly ILogger<AttendanceReportController> _logger;

    public AttendanceReportController(
        IAttendanceReportRepository repository,
        ILogger<AttendanceReportController> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    [HttpGet]
    public async Task<ActionResult<AttendanceReportResponseDto>> GetReport(
        [FromQuery] Guid classId, 
        [FromQuery] int month, 
        [FromQuery] Guid academicYearId, 
        CancellationToken cancellationToken)
    {
        var report = await _repository.GetMonthlyAttendanceReportAsync(classId, month, academicYearId, cancellationToken);

        return Ok(report);
    }
}
