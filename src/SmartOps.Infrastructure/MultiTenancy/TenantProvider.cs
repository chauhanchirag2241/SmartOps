using SmartOps.Application.Common.Abstractions;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantProvider : ITenantProvider
{
    private readonly TenantContext _tenantContext;

    public TenantProvider(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public string? GetCurrentTenantId()
    {
        return _tenantContext.TenantId;
    }

    public string? GetCurrentSchoolId()
    {
        return _tenantContext.SchoolId;
    }
}
