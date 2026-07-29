using System.Data;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.Persistence.Context;

public sealed class DapperContext : IDisposable, IAsyncDisposable
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ITenantSchemaProvider _tenantSchemaProvider;
    private readonly TenantContext _tenantContext;
    private NpgsqlConnection? _connection;
    private NpgsqlConnection? _globalDatabaseConnection;
    private string? _operationalBindingKey;

    public DapperContext(
        IDbConnectionFactory connectionFactory,
        ITenantSchemaProvider tenantSchemaProvider,
        TenantContext tenantContext)
    {
        _connectionFactory = connectionFactory;
        _tenantSchemaProvider = tenantSchemaProvider;
        _tenantContext = tenantContext;
    }

    /// <summary>Operational schema (<c>school</c> on dedicated DB).</summary>
    public string OperationalSchema => _tenantSchemaProvider.GetOperationalSchema();

    /// <summary>
    /// Identity/management schema: <c>man</c> on dedicated school DB, else platform <c>global</c>.
    /// </summary>
    public string IdentitySchema => _tenantSchemaProvider.GetIdentitySchema();

    /// <summary>
    /// Always opens the platform/global catalog database (<c>GlobalDatabase</c>), never the tenant school CS.
    /// </summary>
    public async Task<IDbConnection> GetGlobalDatabaseConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_globalDatabaseConnection is null || IsConnectionDisposed(_globalDatabaseConnection))
        {
            _globalDatabaseConnection = (NpgsqlConnection)await _connectionFactory
                .CreateGlobalDatabaseConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return _globalDatabaseConnection;
    }

    public Task<IDbConnection> GetPlatformConnectionAsync(CancellationToken cancellationToken = default)
    {
        return GetGlobalDatabaseConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Tenant identity/ops connection: dedicated school CS when set, otherwise platform.
    /// </summary>
    public async Task<IDbConnection> GetGlobalConnectionAsync(CancellationToken cancellationToken = default)
    {
        string bindingKey = GetOperationalBindingKey();

        if (_connection is not null
            && (!string.Equals(_operationalBindingKey, bindingKey, StringComparison.Ordinal)
                || IsConnectionDisposed(_connection)))
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_connection is null)
        {
            if (_tenantContext.UsesDedicatedDatabase)
            {
                _connection = (NpgsqlConnection)await _connectionFactory
                    .CreateConnectionAsync(_tenantContext.ConnectionString!, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _connection = (NpgsqlConnection)await _connectionFactory
                    .CreateGlobalDatabaseConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            _operationalBindingKey = bindingKey;
        }

        return _connection;
    }

    private string GetOperationalBindingKey()
    {
        if (_tenantContext.UsesDedicatedDatabase)
        {
            return "school:" + _tenantContext.ConnectionString;
        }

        return "platform";
    }

    /// <summary>
    /// Scoped connection is owned by DapperContext — callers must not dispose it.
    /// </summary>
    private static bool IsConnectionDisposed(IDbConnection connection)
    {
        if (connection is not NpgsqlConnection npgsql)
        {
            return false;
        }

        try
        {
            _ = npgsql.State;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    public Task<IDbConnection> GetOperationalConnectionAsync(CancellationToken cancellationToken = default)
    {
        return GetGlobalConnectionAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_connection is not null)
        {
            _connection.Dispose();
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_globalDatabaseConnection is not null)
        {
            _globalDatabaseConnection.Dispose();
            _globalDatabaseConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
            _operationalBindingKey = null;
        }

        if (_globalDatabaseConnection is not null)
        {
            await _globalDatabaseConnection.DisposeAsync().ConfigureAwait(false);
            _globalDatabaseConnection = null;
        }
    }
}
