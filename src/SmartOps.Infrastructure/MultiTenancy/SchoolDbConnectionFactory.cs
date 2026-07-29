using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class SchoolDbConnectionFactory : ISchoolDbConnectionFactory
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DapperContext _context;

    public SchoolDbConnectionFactory(IDbConnectionFactory connectionFactory, DapperContext context)
    {
        _connectionFactory = connectionFactory;
        _context = context;
    }

    public async Task<string?> GetConnectionStringAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection platform = await _context.GetGlobalDatabaseConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        return await platform.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                $"""
SELECT connectionstring
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE id = @SchoolId AND isactive = true
LIMIT 1;
""",
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IDbConnection> OpenBySchoolIdAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        string? connectionString = await GetConnectionStringAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"School {schoolId} has no dedicated database connection string.");
        }

        return await OpenAsync(connectionString, cancellationToken).ConfigureAwait(false);
    }

    public Task<IDbConnection> OpenAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("School connection string is required.", nameof(connectionString));
        }

        return _connectionFactory.CreateConnectionAsync(connectionString, cancellationToken);
    }
}
