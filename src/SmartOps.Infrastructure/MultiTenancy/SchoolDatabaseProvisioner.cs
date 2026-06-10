using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Configuration;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class SchoolDatabaseProvisioner : ISchoolDatabaseProvisioner
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDatabaseMigrationService _migrationService;
    private readonly IOptions<PerSchoolDatabaseOptions> _options;
    private readonly SchoolDatabaseSeedService _seedService;
    private readonly ILogger<SchoolDatabaseProvisioner> _logger;

    public SchoolDatabaseProvisioner(
        IDbConnectionFactory connectionFactory,
        IDatabaseMigrationService migrationService,
        IOptions<PerSchoolDatabaseOptions> options,
        SchoolDatabaseSeedService seedService,
        ILogger<SchoolDatabaseProvisioner> logger)
    {
        _connectionFactory = connectionFactory;
        _migrationService = migrationService;
        _options = options;
        _seedService = seedService;
        _logger = logger;
    }

    public async Task<(string DatabaseName, string ConnectionString)> ProvisionAsync(
        Guid schoolId,
        string subdomain,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Value.Enabled)
        {
            throw new InvalidOperationException("Per-school database provisioning is disabled.");
        }

        string platformConnectionString = await _connectionFactory
            .GetPlatformConnectionStringAsync(cancellationToken)
            .ConfigureAwait(false);

        string databaseName = SchoolDatabaseConnectionBuilder.BuildDatabaseName(
            _options.Value.DatabaseNamePrefix,
            subdomain);

        string schoolConnectionString = SchoolDatabaseConnectionBuilder.BuildConnectionString(
            platformConnectionString,
            databaseName);

        await CreateDatabaseIfNotExistsAsync(platformConnectionString, databaseName, cancellationToken)
            .ConfigureAwait(false);

        await _migrationService
            .MigrateSchoolDatabaseAsync(schoolConnectionString, cancellationToken)
            .ConfigureAwait(false);

        await using NpgsqlConnection platform = (NpgsqlConnection)await _connectionFactory
            .CreatePlatformConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using NpgsqlConnection schoolDb = new(schoolConnectionString);
        await schoolDb.OpenAsync(cancellationToken).ConfigureAwait(false);

        await _seedService.SeedDefaultsAsync(platform, schoolDb, schoolId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Provisioned dedicated database {DatabaseName} for school {SchoolId}.",
            databaseName,
            schoolId);

        return (databaseName, schoolConnectionString);
    }

    private async Task CreateDatabaseIfNotExistsAsync(
        string platformConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        string adminConnectionString = SchoolDatabaseConnectionBuilder.BuildAdminConnectionString(
            platformConnectionString);

        await using NpgsqlConnection admin = new(adminConnectionString);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        bool exists = await admin.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @Name);",
                new { Name = databaseName },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (exists)
        {
            _logger.LogInformation("Database {DatabaseName} already exists; applying pending migrations.", databaseName);
            return;
        }

        string createSql = $"""CREATE DATABASE "{databaseName}" ENCODING 'UTF8';""";
        await admin.ExecuteAsync(
            new CommandDefinition(createSql, cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation("Created database {DatabaseName}.", databaseName);
    }
}
