using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartOps.Application.Abstractions;

namespace SmartOps.Infrastructure.Persistence.Factories;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _platformConnectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        string? cs = configuration.GetConnectionString("PlatformDb")
            ?? configuration.GetConnectionString("GlobalDb");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("Connection string 'PlatformDb' or 'GlobalDb' is not configured.");
        }

        _platformConnectionString = cs;
    }

    public Task<IDbConnection> CreatePlatformConnectionAsync(CancellationToken cancellationToken = default)
    {
        return OpenConnectionAsync(_platformConnectionString, cancellationToken);
    }

    public Task<IDbConnection> CreateGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        return CreatePlatformConnectionAsync(cancellationToken);
    }

    public Task<IDbConnection> CreateTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        return CreatePlatformConnectionAsync(cancellationToken);
    }

    public Task<IDbConnection> CreateConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        return OpenConnectionAsync(connectionString, cancellationToken);
    }

    public Task<string> GetPlatformConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(_platformConnectionString);
    }

    private static async Task<IDbConnection> OpenConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
