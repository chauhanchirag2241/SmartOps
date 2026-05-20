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
}
