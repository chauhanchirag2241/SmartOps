namespace SmartOps.Application.Abstractions;

public interface ITenantProvisioningService
{
    Task ProvisionSchemaAsync(string schemaName, CancellationToken cancellationToken = default);
}
