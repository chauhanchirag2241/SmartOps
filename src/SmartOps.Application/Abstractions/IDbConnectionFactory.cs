using System.Data;

namespace SmartOps.Application.Abstractions;

public interface IDbConnectionFactory
{
    /// <summary>Platform registry database (schools, platform admins).</summary>
    Task<IDbConnection> CreatePlatformConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Alias for platform connection — identity on platform or tenant DB depending on caller.</summary>
    Task<IDbConnection> CreateGlobalConnectionAsync(CancellationToken cancellationToken = default);

    Task<IDbConnection> CreateTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<IDbConnection> CreateConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<string> GetPlatformConnectionStringAsync(CancellationToken cancellationToken = default);
}
