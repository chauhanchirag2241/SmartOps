using SmartOps.Application.Modules.Tenant.DTOs;
using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Tenant.Interfaces;

public interface ITenantService
{
    Task<Result<TenantDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<TenantDto>> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);
}
