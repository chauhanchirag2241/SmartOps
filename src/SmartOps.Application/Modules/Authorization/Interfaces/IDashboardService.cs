using SmartOps.Application.Modules.Authorization;

namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
