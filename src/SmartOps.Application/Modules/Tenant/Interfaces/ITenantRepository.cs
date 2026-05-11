using TenantEntity = SmartOps.Domain.Modules.Tenant.Entities.Tenant;

namespace SmartOps.Application.Modules.Tenant.Interfaces;

public interface ITenantRepository
{
    Task<TenantEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TenantEntity?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);
}
