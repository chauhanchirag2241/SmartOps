using SmartOps.Application.Modules.Authorization;

namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken cancellationToken = default);

    Task<DashboardResponseDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
