using System.Data;

namespace SmartOps.Application.Abstractions;

public interface IDbConnectionFactory
{
    /// <summary>Platform / global catalog database (<c>smartops_global</c>) — always, never tenant CS.</summary>
    Task<IDbConnection> CreateGlobalDatabaseConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Alias for <see cref="CreateGlobalDatabaseConnectionAsync"/>.</summary>
    Task<IDbConnection> CreatePlatformConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Alias for platform/global database connection.</summary>
    Task<IDbConnection> CreateGlobalConnectionAsync(CancellationToken cancellationToken = default);

    Task<IDbConnection> CreateTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IDbConnection> CreateConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<string> GetPlatformConnectionStringAsync(CancellationToken cancellationToken = default);
}
