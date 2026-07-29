using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.School;
using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class SchoolDataMigrationService : ISchoolDataMigrationService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ISchoolDatabaseProvisioner _provisioner;
    private readonly ILogger<SchoolDataMigrationService> _logger;

    public SchoolDataMigrationService(
        IDbConnectionFactory connectionFactory,
        ISchoolRepository schoolRepository,
        ISchoolDatabaseProvisioner provisioner,
        ILogger<SchoolDataMigrationService> logger)
    {
        _connectionFactory = connectionFactory;
        _schoolRepository = schoolRepository;
        _provisioner = provisioner;
        _logger = logger;
    }

    public async Task MigrateSchoolToDedicatedDatabaseAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        SchoolEntity? school = await _schoolRepository
            .GetSchoolByIdAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        if (school is null)
        {
            throw new InvalidOperationException($"School {schoolId} was not found.");
        }

        if (!string.IsNullOrWhiteSpace(school.ConnectionString))
        {
            _logger.LogInformation("School {SchoolId} already uses a dedicated database.", schoolId);
            return;
        }

        string tenantSchema = school.SchemaName
            ?? $"school_{school.Subdomain.Replace('-', '_')}";

        (string databaseName, string connectionString) = await _provisioner
            .ProvisionAsync(schoolId, school.Subdomain, cancellationToken)
            .ConfigureAwait(false);

        await using NpgsqlConnection platform = (NpgsqlConnection)await _connectionFactory
            .CreatePlatformConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using NpgsqlConnection schoolDb = new(connectionString);
        await schoolDb.OpenAsync(cancellationToken).ConfigureAwait(false);

        PostgresDataCopier dataCopier = new(_logger);
        string man = DatabaseConfig.Schema_Man;

        // Catalog (menus/widgets/usertypes) stays on platform — provision seed grants Admin permissions.
        // Copy operational tables from legacy shared tenant schema into school.school.
        foreach (string table in SchoolSchemaCatalog.TemplateTables)
        {
            await dataCopier.CopyAllRowsAsync(
                platform,
                tenantSchema,
                schoolDb,
                DatabaseConfig.Schema_School,
                table,
                cancellationToken).ConfigureAwait(false);
        }

        // Best-effort: if legacy platform still has schoolsettings/branches for this school, copy into man.
        await dataCopier.CopyTableDataAsync(
            platform,
            DatabaseConfig.Schema_Global,
            schoolDb,
            man,
            DatabaseConfig.TableSchoolSettings,
            "schoolid = @SchoolId",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            DatabaseConfig.Schema_Global,
            schoolDb,
            man,
            DatabaseConfig.TableSchoolBranches,
            "schoolid = @SchoolId",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        school.DatabaseName = databaseName;
        school.ConnectionString = connectionString;
        await _schoolRepository.UpdateSchoolConnectionAsync(school, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Migrated school {SchoolId} from schema {Schema} to database {DatabaseName}.",
            schoolId,
            tenantSchema,
            databaseName);
    }
}
