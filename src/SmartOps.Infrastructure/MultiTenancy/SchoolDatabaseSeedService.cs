using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Seeds school-local identity defaults (Admin role, leave settings, Admin menu/widget permissions).
/// Catalog tables (menus, widgets, usertypes) stay on the platform global database.
/// </summary>
public sealed class SchoolDatabaseSeedService
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid AdminRoleId = Guid.Parse("20000000-0000-0000-0000-000000000001");

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
        await EnsureAdminRoleAsync(schoolDb, cancellationToken).ConfigureAwait(false);
        await SeedLeaveSettingsAsync(schoolDb, schoolId, cancellationToken).ConfigureAwait(false);
        await GrantAdminSchoolMenuPermissionsAsync(platform, schoolDb, cancellationToken).ConfigureAwait(false);
        await GrantAdminWidgetPermissionsAsync(platform, schoolDb, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Seeded school-local identity defaults for school {SchoolId}.", schoolId);
    }

    private static async Task EnsureAdminRoleAsync(
        NpgsqlConnection schoolDb,
        CancellationToken cancellationToken)
    {
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = DateTime.UtcNow;

        string sql = $"""
INSERT INTO {man}.{DatabaseConfig.TableRoles}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @Id, @Name, @Description, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) = lower(trim(@Name))
);
""";
        await schoolDb.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = AdminRoleId,
                    Name = RoleNames.Admin,
                    Description = "Default administrator role",
                    Actor = SeedActor,
                    Now = utcNow,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task GrantAdminSchoolMenuPermissionsAsync(
        NpgsqlConnection platform,
        NpgsqlConnection schoolDb,
        CancellationToken cancellationToken)
    {
        string g = DatabaseConfig.Schema_Global;
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = DateTime.UtcNow;

        string menusSql = $"""
SELECT id
FROM {g}.{DatabaseConfig.TableMenus}
WHERE isactive = true
  AND application IN (@SchoolApp, @CommonApp)
""";
        List<Guid> menuIds = (await platform.QueryAsync<Guid>(
            new CommandDefinition(
                menusSql,
                new { SchoolApp = MenuApplications.School, CommonApp = MenuApplications.Common },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (menuIds.Count == 0)
        {
            return;
        }

        Guid? adminRoleId = await schoolDb.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {man}.{DatabaseConfig.TableRoles}
WHERE lower(trim(name)) = lower(trim(@Name)) AND isactive = true
LIMIT 1
""",
                new { Name = RoleNames.Admin },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (adminRoleId is null || adminRoleId == Guid.Empty)
        {
            return;
        }

        string insertSql = $"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @RoleId, @MenuId, true, true, true, true, true, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions}
    WHERE roleid = @RoleId AND menuid = @MenuId
);
""";

        foreach (Guid menuId in menuIds)
        {
            await schoolDb.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { RoleId = adminRoleId, MenuId = menuId, Actor = SeedActor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private static async Task GrantAdminWidgetPermissionsAsync(
        NpgsqlConnection platform,
        NpgsqlConnection schoolDb,
        CancellationToken cancellationToken)
    {
        string g = DatabaseConfig.Schema_Global;
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = DateTime.UtcNow;

        List<Guid> widgetIds = (await platform.QueryAsync<Guid>(
            new CommandDefinition(
                $"""
SELECT id FROM {g}.{DatabaseConfig.TableDashboardWidgets}
WHERE isactive = true
""",
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (widgetIds.Count == 0)
        {
            return;
        }

        Guid? adminRoleId = await schoolDb.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {man}.{DatabaseConfig.TableRoles}
WHERE lower(trim(name)) = lower(trim(@Name)) AND isactive = true
LIMIT 1
""",
                new { Name = RoleNames.Admin },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (adminRoleId is null || adminRoleId == Guid.Empty)
        {
            return;
        }

        string insertSql = $"""
INSERT INTO {man}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    (id, roleid, widgetid, canview, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @RoleId, @WidgetId, true, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    WHERE roleid = @RoleId AND widgetid = @WidgetId
);
""";

        foreach (Guid widgetId in widgetIds)
        {
            await schoolDb.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { RoleId = adminRoleId, WidgetId = widgetId, Actor = SeedActor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private static async Task SeedLeaveSettingsAsync(
        NpgsqlConnection schoolDb,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = DateTime.UtcNow;
        (string Key, string Value)[] defaults =
        [
            (LeaveSettingKeys.StaffApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StaffApproverUserTypes, UserTypeCodes.OfficeStaff),
            (LeaveSettingKeys.StudentApprovalMode, LeaveApprovalModes.AnyOne),
            (LeaveSettingKeys.StudentDefaultApprover, LeaveApproverTokens.ClassTeacher),
            (LeaveSettingKeys.StudentLongLeaveMinDays, "4"),
            (LeaveSettingKeys.StudentLongLeaveApproverUserTypes, UserTypeCodes.OfficeStaff),
            (LeaveSettingKeys.StudentLongLeaveTransferToPrincipal, "true"),
        ];

        foreach ((string key, string value) in defaults)
        {
            string insertSql = $"""
INSERT INTO {man}.{DatabaseConfig.TableSchoolSettings}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @SchoolId, @Key, @Value, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableSchoolSettings}
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
