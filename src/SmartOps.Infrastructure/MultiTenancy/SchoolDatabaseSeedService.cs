using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Seeds identity defaults (roles, user types, menus) into a new dedicated school database.
/// </summary>
public sealed class SchoolDatabaseSeedService
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private readonly ILogger<SchoolDatabaseSeedService> _logger;

    public SchoolDatabaseSeedService(ILogger<SchoolDatabaseSeedService> logger)
    {
        _logger = logger;
    }

    public async Task SeedDefaultsAsync(
        NpgsqlConnection platform,
        NpgsqlConnection schoolDb,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        string g = DatabaseConfig.Schema_Global;
        PostgresDataCopier copier = new(_logger);

        await copier.CopyAllRowsAsync(platform, g, schoolDb, g, DatabaseConfig.TableUserTypes, cancellationToken)
            .ConfigureAwait(false);
        await copier.CopyAllRowsAsync(platform, g, schoolDb, g, DatabaseConfig.TableRoles, cancellationToken)
            .ConfigureAwait(false);
        await copier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableMenus,
            "application IN (@SchoolApp, @CommonApp)",
            new { SchoolApp = MenuApplications.School, CommonApp = MenuApplications.Common },
            cancellationToken).ConfigureAwait(false);
        await copier.CopyTableDataAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableRoleMenuPermissions,
            $"""
menuid IN (
    SELECT id FROM {g}.{DatabaseConfig.TableMenus}
    WHERE application IN (@SchoolApp, @CommonApp)
)
""",
            new { SchoolApp = MenuApplications.School, CommonApp = MenuApplications.Common },
            cancellationToken).ConfigureAwait(false);
        await copier.CopyAllRowsAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableDashboardWidgets,
            cancellationToken).ConfigureAwait(false);
        await copier.CopyAllRowsAsync(
            platform,
            g,
            schoolDb,
            g,
            DatabaseConfig.TableRoleDashboardWidgetPermissions,
            cancellationToken).ConfigureAwait(false);

        await SeedLeaveSettingsAsync(schoolDb, schoolId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Seeded default identity data for school {SchoolId}.", schoolId);
    }

    private static async Task SeedLeaveSettingsAsync(
        NpgsqlConnection schoolDb,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        string g = DatabaseConfig.Schema_Global;
        DateTime utcNow = DateTime.UtcNow;
        (string Key, string Value)[] defaults =
        [
            (LeaveSettingKeys.StaffApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StaffApproverUserTypes, UserTypeCodes.SchoolAdmin),
            (LeaveSettingKeys.StudentApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StudentDefaultApprover, LeaveApproverTokens.ClassTeacher),
            (LeaveSettingKeys.StudentLongLeaveMinDays, "4"),
            (LeaveSettingKeys.StudentLongLeaveApproverUserTypes, UserTypeCodes.Principal),
            (LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, "true"),
        ];

        foreach ((string key, string value) in defaults)
        {
            string insertSql = $"""
INSERT INTO {g}.{DatabaseConfig.TableSchoolSettings}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @SchoolId, @Key, @Value, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableSchoolSettings}
    WHERE schoolid = @SchoolId AND settingkey = @Key
);
""";
            await schoolDb.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { SchoolId = schoolId, Key = key, Value = value, Actor = SeedActor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }
}
