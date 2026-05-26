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
    public async Task<ActionResult<DashboardResponseDto>> GetDashboard(CancellationToken cancellationToken)
    {
        DashboardResponseDto dashboard = await dashboardService
            .GetDashboardAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Ok(dashboard);
    }
}
