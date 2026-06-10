using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(ILogger<TenantProvisioningService> logger)
    {
        _logger = logger;
    }

    public Task ProvisionSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Shared tenant schema provisioning is no longer supported (schema: {Schema}). Enable PerSchoolDatabase to create a dedicated school database.",
            schemaName);

        throw new NotSupportedException(
            "Shared tenant schemas are no longer supported. Enable PerSchoolDatabase so migrations run on a dedicated school database.");
    }
}
