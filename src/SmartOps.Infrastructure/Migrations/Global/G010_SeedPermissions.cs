using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(10, "Global — seed menus")]
public sealed class G010_SeedMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly Guid AcademicsId = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid AcademicSetupId = Guid.Parse("10000000-0000-0000-0000-000000000045");
    private static readonly Guid FeesManagementId = Guid.Parse("10000000-0000-0000-0000-000000000040");
    private static readonly Guid SalaryManagementId = Guid.Parse("10000000-0000-0000-0000-000000000041");
    private static readonly Guid LeaveManagementId = Guid.Parse("10000000-0000-0000-0000-000000000042");
    private static readonly Guid AdministrationId = Guid.Parse("10000000-0000-0000-0000-000000000043");
    private static readonly Guid ReportsId = Guid.Parse("10000000-0000-0000-0000-000000000044");

    private static readonly (Guid Id, string Name, string Code, string Application, Guid? ParentId, string? Route, string? Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000001"), "Dashboard", MenuCodes.Dashboard, MenuApplications.Common, null, "/dashboard", "dashboard", 1),
        (Guid.Parse("10000000-0000-0000-0000-000000000002"), "Schools", MenuCodes.Schools, MenuApplications.Config, null, "/configuration/schools", "school", 2),
        (Guid.Parse("10000000-0000-0000-0000-000000000003"), "Users", MenuCodes.Users, MenuApplications.Config, null, "/configuration/users", "group", 3),
        (Guid.Parse("10000000-0000-0000-0000-000000000004"), "Roles", MenuCodes.Roles, MenuApplications.Config, null, "/configuration/roles", "admin_panel_settings", 4),
        (Guid.Parse("10000000-0000-0000-0000-000000000005"), "Settings", MenuCodes.Settings, MenuApplications.Config, null, "/settings", "settings", 5),

        // School sidebar group roots (no route)
        (AcademicsId, "Academics", MenuCodes.Academics, MenuApplications.School, null, null, "menu_book", 10),
        (AcademicSetupId, "Academic Setup", MenuCodes.AcademicSetup, MenuApplications.School, null, null, "tune", 15),
        (FeesManagementId, "Fee", MenuCodes.FeesManagement, MenuApplications.School, null, null, "payments", 20),
        (SalaryManagementId, "Salary", MenuCodes.SalaryManagement, MenuApplications.School, null, null, "account_balance_wallet", 30),
        (LeaveManagementId, "Leave", MenuCodes.LeaveManagement, MenuApplications.School, null, null, "event_busy", 40),
        (AdministrationId, "Administration", MenuCodes.Administration, MenuApplications.School, null, null, "admin_panel_settings", 50),
        (ReportsId, "Reports", MenuCodes.Reports, MenuApplications.School, null, null, "analytics", 60),

        // Academics children
        (Guid.Parse("10000000-0000-0000-0000-000000000011"), "Students", MenuCodes.Students, MenuApplications.School, AcademicsId, "/students", "groups", 11),
        (Guid.Parse("10000000-0000-0000-0000-000000000016"), "Attendance", MenuCodes.Attendance, MenuApplications.School, AcademicsId, "/attendance", "how_to_reg", 12),

        // Academic Setup children
        (Guid.Parse("10000000-0000-0000-0000-000000000015"), "Academic Years", MenuCodes.AcademicYears, MenuApplications.School, AcademicSetupId, "/academic-years", "calendar_month", 16),
        (Guid.Parse("10000000-0000-0000-0000-000000000013"), "Classes", MenuCodes.Classes, MenuApplications.School, AcademicSetupId, "/classes", "class", 17),
        (Guid.Parse("10000000-0000-0000-0000-000000000014"), "Subjects", MenuCodes.Subjects, MenuApplications.School, AcademicSetupId, "/subjects", "subject", 18),

        // Administration children (Users/Roles/Notices seeded in later migrations)
        (Guid.Parse("10000000-0000-0000-0000-000000000012"), "Employees", MenuCodes.Employees, MenuApplications.School, AdministrationId, "/employees", "co_present", 50),
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
