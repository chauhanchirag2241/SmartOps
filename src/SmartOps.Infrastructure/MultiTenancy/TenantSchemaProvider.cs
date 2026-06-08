using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantSchemaProvider : ITenantSchemaProvider
{
    private readonly TenantContext _tenantContext;

    public TenantSchemaProvider(TenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public string GetOperationalSchema()
    {
        if (_tenantContext.UsesDedicatedDatabase)
        {
            return DatabaseConfig.Schema_School;
        }

        if (!string.IsNullOrWhiteSpace(_tenantContext.SchemaName))
        {
            return _tenantContext.SchemaName;
        }

        return DatabaseConfig.Schema_Global;
    }

    public bool IsTenantScoped =>
        _tenantContext.UsesDedicatedDatabase || !string.IsNullOrWhiteSpace(_tenantContext.SchemaName);
}
