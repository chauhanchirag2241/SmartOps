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
        string g = DatabaseConfig.Schema_Global;

        await dataCopier.CopyAllRowsAsync(platform, g, schoolDb, g, DatabaseConfig.TableUserTypes, cancellationToken)
            .ConfigureAwait(false);
        await dataCopier.CopyAllRowsAsync(platform, g, schoolDb, g, DatabaseConfig.TableRoles, cancellationToken)
            .ConfigureAwait(false);
        await dataCopier.CopyAllRowsAsync(platform, g, schoolDb, g, DatabaseConfig.TableMenus, cancellationToken)
            .ConfigureAwait(false);
        await dataCopier.CopyAllRowsAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableRoleMenuPermissions,
            cancellationToken).ConfigureAwait(false);
        await dataCopier.CopyAllRowsAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableDashboardWidgets,
            cancellationToken).ConfigureAwait(false);
        await dataCopier.CopyAllRowsAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableRoleDashboardWidgetPermissions,
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableUsers,
            """
u.id IN (
    SELECT m.userid FROM global.userschoolmappings m
    WHERE m.schoolid = @SchoolId AND m.isactive = true
)
""",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableUserRoles,
            """
ur.userid IN (
    SELECT m.userid FROM global.userschoolmappings m
    WHERE m.schoolid = @SchoolId AND m.isactive = true
)
""",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableUserSchoolMappings,
            "schoolid = @SchoolId",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableSchoolSettings,
            "schoolid = @SchoolId",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableRefreshTokens,
            """
userid IN (
    SELECT m.userid FROM global.userschoolmappings m
    WHERE m.schoolid = @SchoolId AND m.isactive = true
)
""",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

        await dataCopier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableUserScopeVersions,
            """
userid IN (
    SELECT m.userid FROM global.userschoolmappings m
    WHERE m.schoolid = @SchoolId AND m.isactive = true
)
""",
            new { SchoolId = schoolId },
            cancellationToken).ConfigureAwait(false);

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
