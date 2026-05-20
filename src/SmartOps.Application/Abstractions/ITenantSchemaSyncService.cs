namespace SmartOps.Application.Abstractions;

public interface ITenantSchemaSyncService
{
    /// <summary>
    /// Applies the current <c>school</c> template schema to every active tenant schema.
    /// </summary>
    Task SyncAllActiveSchoolSchemasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates missing tables and columns in a tenant schema from the <c>school</c> template.
    /// </summary>
    Task SyncTenantSchemaAsync(string schemaName, CancellationToken cancellationToken = default);
}
