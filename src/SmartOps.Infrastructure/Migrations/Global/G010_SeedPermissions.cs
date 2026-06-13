using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(10, "Global — seed menus")]
public sealed class G010_SeedMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Name, string Code, string Application, Guid? ParentId, string? Route, string? Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000001"), "Dashboard", MenuCodes.Dashboard, MenuApplications.Common, null, "/dashboard", "dashboard", 1),
        (Guid.Parse("10000000-0000-0000-0000-000000000002"), "Schools", MenuCodes.Schools, MenuApplications.Config, null, "/configuration/schools", "school", 2),
        (Guid.Parse("10000000-0000-0000-0000-000000000003"), "Users", MenuCodes.Users, MenuApplications.Config, null, "/configuration/users", "group", 3),
        (Guid.Parse("10000000-0000-0000-0000-000000000004"), "Roles", MenuCodes.Roles, MenuApplications.Config, null, "/configuration/roles", "admin_panel_settings", 4),
        (Guid.Parse("10000000-0000-0000-0000-000000000005"), "Settings", MenuCodes.Settings, MenuApplications.Config, null, "/settings", "settings", 5),
        (Guid.Parse("10000000-0000-0000-0000-000000000010"), "Academics", MenuCodes.Academics, MenuApplications.School, null, null, "menu_book", 10),
        (Guid.Parse("10000000-0000-0000-0000-000000000011"), "Students", MenuCodes.Students, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/students", "groups", 11),
        (Guid.Parse("10000000-0000-0000-0000-000000000012"), "Employees", MenuCodes.Employees, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/employees", "co_present", 12),
        (Guid.Parse("10000000-0000-0000-0000-000000000013"), "Classes", MenuCodes.Classes, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/classes", "class", 13),
        (Guid.Parse("10000000-0000-0000-0000-000000000014"), "Subjects", MenuCodes.Subjects, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/subjects", "subject", 14),
        (Guid.Parse("10000000-0000-0000-0000-000000000015"), "Academic Years", MenuCodes.AcademicYears, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/academic-years", "calendar_month", 15),
        (Guid.Parse("10000000-0000-0000-0000-000000000016"), "Attendance", MenuCodes.Attendance, MenuApplications.School, Guid.Parse("10000000-0000-0000-0000-000000000010"), "/attendance", "how_to_reg", 16),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, string application, Guid? parentId, string? route, string? icon, int order) in Menus)
        {
            string parentSql = parentId.HasValue ? $"'{parentId}'" : "NULL";
            string routeSql = route is null ? "NULL" : $"'{route}'";
            string iconSql = icon is null ? "NULL" : $"'{icon}'";

            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', {parentSql}, {routeSql}, {iconSql}, {order}, '{application}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code = '{code}' AND application = '{application}'
);
""");
        }
    }

    public override void Down()
    {
        string codes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
WHERE code IN ('{codes}');
""");
    }
}
