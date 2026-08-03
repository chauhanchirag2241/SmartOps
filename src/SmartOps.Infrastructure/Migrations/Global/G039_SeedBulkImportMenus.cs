using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(39, "Global — seed bulk import parent and student bulk import menus")]
public sealed class G039_SeedBulkImportMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid BulkImportMenuId = Guid.Parse("10000000-0000-0000-0000-000000000088");
    private static readonly Guid StudentBulkImportMenuId = Guid.Parse("10000000-0000-0000-0000-000000000089");

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{BulkImportMenuId}', 'Bulk Import', '{MenuCodes.BulkImport}', NULL,
       NULL, 'upload_file', 25, '{MenuApplications.School}', true, 1,
       '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.BulkImport}'
);
""");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{StudentBulkImportMenuId}', 'Student Bulk Import', '{MenuCodes.StudentBulkImport}', '{BulkImportMenuId}',
       '/bulk-import/students', 'group_add', 1, '{MenuApplications.School}', true, 1,
       '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.StudentBulkImport}'
);
""");

        foreach (string menuCode in new[] { MenuCodes.BulkImport, MenuCodes.StudentBulkImport })
        {
            bool fullAccess = menuCode == MenuCodes.StudentBulkImport;
            string canAdd = fullAccess ? "true" : "false";
            string canEdit = fullAccess ? "true" : "false";
            string canDelete = fullAccess ? "true" : "false";
            string canExport = fullAccess ? "true" : "false";

            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, {canAdd}, {canEdit}, {canDelete}, {canExport}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code = '{menuCode}'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (
    SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code IN ('{MenuCodes.StudentBulkImport}', '{MenuCodes.BulkImport}')
);
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.StudentBulkImport}';
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.BulkImport}';
""");
    }
}
