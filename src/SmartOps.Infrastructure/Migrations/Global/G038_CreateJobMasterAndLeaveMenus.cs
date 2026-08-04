using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Migrations.Extensions;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(38, "Global — job definitions, hangfire config, leave/job menus")]
public sealed class G038_CreateJobMasterAndLeaveMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid LeaveManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000042");
    private static readonly Guid HangfireConfigId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid MonthlyJobId = Guid.Parse("b1000000-0000-0000-0000-000000000002");

    private static readonly (Guid Id, string Name, string Code, Guid? ParentId, string? Route, string Icon, int Order, string App)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000084"), "Leave Types", MenuCodes.LeaveTypes, LeaveManagementParentId, "/leave/types", "category", 43, MenuApplications.School),
        (Guid.Parse("10000000-0000-0000-0000-000000000085"), "Leave Policies", MenuCodes.LeavePolicies, LeaveManagementParentId, "/leave/policies", "rule", 44, MenuApplications.School),
        (Guid.Parse("10000000-0000-0000-0000-000000000086"), "Leave Balances", MenuCodes.LeaveBalances, LeaveManagementParentId, "/leave/balances", "account_balance_wallet", 45, MenuApplications.School),
        (Guid.Parse("10000000-0000-0000-0000-000000000087"), "Job Master", MenuCodes.JobMaster, null, "/configuration/jobs", "schedule", 6, MenuApplications.Config),
    ];

    public override void Up()
    {
        string g = DatabaseConfig.Schema_Global;

        if (!Schema.Schema(g).Table(DatabaseConfig.TableHangfireConfig).Exists())
        {
            Create.Table(DatabaseConfig.TableHangfireConfig).InSchema(g)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
                .WithColumn("isenabled").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("updatedby").AsGuid().NotNullable().WithDefaultValue(SeedActor)
                .WithColumn("updatedon").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
        }

        if (!Schema.Schema(g).Table(DatabaseConfig.TableJobDefinitions).Exists())
        {
            Create.Table(DatabaseConfig.TableJobDefinitions).InSchema(g)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("code").AsString(50).NotNullable()
                .WithColumn("name").AsString(150).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("cronexpression").AsString(100).NotNullable()
                .WithColumn("timezoneid").AsString(100).NotNullable().WithDefaultValue("India Standard Time")
                .WithColumn("isenabled").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("sortorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_jobdefinitions_code")
                .OnTable(DatabaseConfig.TableJobDefinitions).WithSchema(g)
                .Column("code");
        }

        DateTimeOffset now = SchoolLocalTime.Now();

        Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableHangfireConfig} (id, isenabled, updatedby, updatedon)
SELECT '{HangfireConfigId}', true, '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {g}.{DatabaseConfig.TableHangfireConfig});
""");

        Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableJobDefinitions}
    (id, code, name, description, cronexpression, timezoneid, isenabled, sortorder,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{MonthlyJobId}', '{JobCodes.MonthlyLeaveAccrual}', 'Monthly Leave Accrual',
       'Credits monthly leave to employees per school leave policies.',
       '0 0 1 * *', 'India Standard Time', true, 1,
       true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableJobDefinitions} WHERE code = '{JobCodes.MonthlyLeaveAccrual}'
);
""");

        foreach ((Guid id, string name, string code, Guid? parentId, string? route, string icon, int order, string app) in Menus)
        {
            string parentSql = parentId.HasValue ? $"'{parentId}'" : "NULL";
            string routeSql = route is null ? "NULL" : $"'{route}'";
            Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', {parentSql}, {routeSql}, '{icon}', {order}, '{app}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");
        }

        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {g}.{DatabaseConfig.TableRoles} r
CROSS JOIN {g}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code IN ('{menuCodes}')
  AND NOT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    public override void Down()
    {
        string codes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}'));
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}');
""");
    }
}
