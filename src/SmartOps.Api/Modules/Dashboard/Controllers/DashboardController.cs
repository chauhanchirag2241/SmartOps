using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;

namespace SmartOps.Api.Modules.Dashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        DashboardSummaryDto summary = await dashboardService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        return Ok(summary);
    }

    [HttpGet("layout")]
    [ProducesResponseType(typeof(DashboardLayoutDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardLayoutDto>> GetLayout(CancellationToken cancellationToken)
    {
        DashboardLayoutDto layout = await dashboardService.GetLayoutAsync(cancellationToken).ConfigureAwait(false);
        return Ok(layout);
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponseDto>> GetDashboard(
        [FromQuery] string? attendancePreset,
        [FromQuery] DateOnly? attendanceFrom,
        [FromQuery] DateOnly? attendanceTo,
        CancellationToken cancellationToken)
    {
        var query = new DashboardQueryDto
        {
            AttendancePreset = attendancePreset,
            AttendanceFrom = attendanceFrom,
            AttendanceTo = attendanceTo
        };

        DashboardResponseDto dashboard = await dashboardService
            .GetDashboardAsync(query, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dashboard);
    }
}
