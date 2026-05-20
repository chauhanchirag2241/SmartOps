using System.Data;

namespace SmartOps.Application.Abstractions;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateGlobalConnectionAsync(CancellationToken cancellationToken = default);

    Task<IDbConnection> CreateTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default);
}
