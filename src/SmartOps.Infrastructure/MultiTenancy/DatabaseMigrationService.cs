using Dapper;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Global;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class DatabaseMigrationService : IDatabaseMigrationService
{
    private const string GlobalMigrationTag = "Global";
    private const string SchoolMigrationTag = "School";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(
        IDbConnectionFactory connectionFactory,
        ILogger<DatabaseMigrationService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task MigrateGlobalDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string connectionString = await _connectionFactory
            .GetPlatformConnectionStringAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Running global database migrations...");
        RunMigrations(connectionString, GlobalMigrationTag);
        _logger.LogInformation("Global database migrations completed.");
    }

    public Task MigrateSchoolDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        _logger.LogInformation("Running school database migrations...");
        RunMigrations(connectionString, SchoolMigrationTag);
        _logger.LogInformation("School database migrations completed.");
        return Task.CompletedTask;
    }

    public async Task MigrateAllSchoolDatabasesAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection platform = (NpgsqlConnection)await _connectionFactory
            .CreatePlatformConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        const string sql = $"""
SELECT id AS Id, connectionstring AS ConnectionString
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE isactive = true
  AND connectionstring IS NOT NULL
  AND TRIM(connectionstring) <> ''
""";

        List<SchoolConnectionRow> schools = (await platform.QueryAsync<SchoolConnectionRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (schools.Count == 0)
        {
            _logger.LogInformation("No dedicated school databases found for migration.");
            return;
        }

        _logger.LogInformation("Applying school migrations to {Count} database(s).", schools.Count);

        foreach (SchoolConnectionRow school in schools)
        {
            try
            {
                await MigrateSchoolDatabaseAsync(school.ConnectionString, cancellationToken).ConfigureAwait(false);
                await VerifySchoolOperationalSchemaAsync(school.ConnectionString, school.Id, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation("School database migrations applied for school {SchoolId}.", school.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply school migrations for school {SchoolId}.", school.Id);
            }
        }
    }

    private async Task VerifySchoolOperationalSchemaAsync(
        string connectionString,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        bool hasAcademicYears = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                $"""
SELECT EXISTS (
    SELECT 1 FROM information_schema.tables
    WHERE table_schema = '{DatabaseConfig.Schema_School}'
      AND table_name = '{DatabaseConfig.TableAcademicYears}')
""",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (hasAcademicYears)
        {
            return;
        }

        long versionCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM public.\"VersionInfo\"",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (versionCount > 0)
        {
            _logger.LogWarning(
                "School {SchoolId} database has migration history ({VersionCount} versions) but is missing {Schema}.{Table}. Resetting school migration history and re-applying.",
                schoolId,
                versionCount,
                DatabaseConfig.Schema_School,
                DatabaseConfig.TableAcademicYears);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM public.\"VersionInfo\" WHERE \"Version\" >= 98",
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            RunMigrations(connectionString, SchoolMigrationTag);
        }
    }

    private void RunMigrations(string connectionString, string migrationTag)
    {
        ServiceProvider serviceProvider = BuildMigrationServiceProvider(connectionString, migrationTag);
        try
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            IMigrationRunner runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            runner.MigrateUp();
        }
        finally
        {
            serviceProvider.Dispose();
        }
    }

    private static ServiceProvider BuildMigrationServiceProvider(string connectionString, string migrationTag)
    {
        return new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(G000_EnablePgCrypto).Assembly)
                .For.Migrations())
            .Configure<RunnerOptions>(options =>
            {
                options.Tags = [migrationTag];
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(validateScopes: false);
    }

    private sealed class SchoolConnectionRow
    {
        public Guid Id { get; init; }
        public string ConnectionString { get; init; } = string.Empty;
    }
}
