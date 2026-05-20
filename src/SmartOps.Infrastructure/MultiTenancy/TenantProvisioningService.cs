using SmartOps.Application.Abstractions;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ITenantSchemaSyncService _schemaSync;

    public TenantProvisioningService(ITenantSchemaSyncService schemaSync)
    {
        _schemaSync = schemaSync;
    }

    public Task ProvisionSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        return _schemaSync.SyncTenantSchemaAsync(schemaName, cancellationToken);
    }
}
