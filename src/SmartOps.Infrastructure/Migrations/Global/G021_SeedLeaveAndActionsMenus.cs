using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(21, "Global — seed leave and actions menus")]
public sealed class G021_SeedLeaveAndActionsMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid LeaveManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000042");
    private static readonly Guid AdministrationParentId = Guid.Parse("10000000-0000-0000-0000-000000000043");
    private static readonly Guid FrontOfficeParentId = Guid.Parse("10000000-0000-0000-0000-000000000050");

    private static readonly (Guid Id, string Name, string Code, Guid? ParentId, string? Route, string Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000026"), "Staff Leave", MenuCodes.LeaveStaff, LeaveManagementParentId, "/leave/staff", "event_busy", 41),
        (Guid.Parse("10000000-0000-0000-0000-000000000027"), "Student Leave", MenuCodes.LeaveStudent, LeaveManagementParentId, "/leave/students", "child_care", 42),
        // Root-level like Dashboard (directly after Dashboard in display order)
        (Guid.Parse("10000000-0000-0000-0000-000000000028"), "My Actions", MenuCodes.MyActions, null, "/my-actions", "pending_actions", 2),
        (Guid.Parse("10000000-0000-0000-0000-000000000029"), "Notices", MenuCodes.Notices, AdministrationParentId, "/notices", "campaign", 53),
        // Front Office (after My Actions)
        (FrontOfficeParentId, "Front Office", MenuCodes.FrontOffice, null, null, "support_agent", 3),
        (Guid.Parse("10000000-0000-0000-0000-000000000051"), "Visitor Book", MenuCodes.VisitorBook, FrontOfficeParentId, "/front-office/visitors", "badge", 31),
        (Guid.Parse("10000000-0000-0000-0000-000000000052"), "Phone Logs", MenuCodes.PhoneLogs, FrontOfficeParentId, "/front-office/phone-logs", "phone", 32),
        (Guid.Parse("10000000-0000-0000-0000-000000000053"), "Complaints", MenuCodes.Complaints, FrontOfficeParentId, "/front-office/complaints", "report_problem", 33),
        (Guid.Parse("10000000-0000-0000-0000-000000000054"), "Admission Inquiries", MenuCodes.AdmissionInquiries, FrontOfficeParentId, "/front-office/admission-inquiries", "school", 34),
        (Guid.Parse("10000000-0000-0000-0000-000000000055"), "Front Office Setup", MenuCodes.FrontOfficeSetup, FrontOfficeParentId, "/front-office/setup", "tune", 35),
    ];

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();

        foreach ((Guid id, string name, string code, Guid? parentId, string? route, string icon, int order) in Menus)
        {
            string parentSql = parentId.HasValue ? $"'{parentId}'" : "NULL";
            string routeSql = route is null ? "NULL" : $"'{route}'";
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', {parentSql}, {routeSql}, '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");
        }

        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code IN ('{menuCodes}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
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
