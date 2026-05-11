using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartOps.Application.Common.Abstractions;

namespace SmartOps.Infrastructure.Persistence.Factories;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _globalConnectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        string? cs = configuration.GetConnectionString("GlobalDb");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Connection string 'GlobalDb' is not configured.");
        }

        _globalConnectionString = cs;
    }

    public async Task<IDbConnection> CreateGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection connection = new(_globalConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    public Task<IDbConnection> CreateTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        return CreateGlobalConnectionAsync(cancellationToken);
    }
}
