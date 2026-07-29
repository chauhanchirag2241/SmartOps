using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>
/// Seeds dashboard widget catalog on platform only.
/// Role↔widget permissions are seeded per school DB (not on global).
/// </summary>
[Tags("Global")]
[Migration(20, "Global — seed dashboard widgets catalog")]
public sealed class G020_SeedDashboardWidgetsAndRolePermissions : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Code, string Name, string Category, string RequiredMenu, int Order, string Size)[] Widgets =
    [
        (Guid.Parse("30000000-0000-0000-0000-000000000001"), DashboardWidgetCodes.StudentsStat, "Total students", "Academics", MenuCodes.Students, 1, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000002"), DashboardWidgetCodes.EmployeesStat, "Total employees", "Academics", MenuCodes.Employees, 2, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000003"), DashboardWidgetCodes.ClassesStat, "Classes / sections", "Academics", MenuCodes.Classes, 3, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000004"), DashboardWidgetCodes.SubjectsStat, "Subjects offered", "Academics", MenuCodes.Subjects, 4, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000007"), DashboardWidgetCodes.SalaryDisbursed, "Salary disbursed", "Finance", MenuCodes.SalaryPayroll, 7, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000008"), DashboardWidgetCodes.AttendanceRate, "Today's attendance", "Academics", MenuCodes.Attendance, 8, "stat"),
        (Guid.Parse("30000000-0000-0000-0000-000000000009"), DashboardWidgetCodes.AttendanceDetail, "Attendance today", "Academics", MenuCodes.Attendance, 9, "chart"),
        (Guid.Parse("30000000-0000-0000-0000-000000000011"), DashboardWidgetCodes.SalaryStatus, "Salary status", "Finance", MenuCodes.SalaryPayroll, 11, "chart"),
        (Guid.Parse("30000000-0000-0000-0000-000000000012"), DashboardWidgetCodes.RecentStudents, "Recent students", "Academics", MenuCodes.Students, 12, "list"),
        (Guid.Parse("30000000-0000-0000-0000-000000000013"), DashboardWidgetCodes.EmployeesList, "Employees", "Academics", MenuCodes.Employees, 13, "list"),
        (Guid.Parse("30000000-0000-0000-0000-000000000014"), DashboardWidgetCodes.HomeworkDue, "Homework due", "Academics", MenuCodes.Homework, 14, "list"),
        (Guid.Parse("30000000-0000-0000-0000-000000000015"), DashboardWidgetCodes.ClassesOverview, "Classes overview", "Academics", MenuCodes.Classes, 15, "grid"),
        (Guid.Parse("30000000-0000-0000-0000-000000000016"), DashboardWidgetCodes.AlertsActions, "Alerts & actions", "Overview", MenuCodes.Dashboard, 16, "composite"),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string code, string name, string category, string requiredMenu, int order, string size) in Widgets)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets}
    (id, code, name, category, requiredmenucode, displayorder, defaultsize, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{EscapeSql(code)}', '{EscapeSql(name)}', '{EscapeSql(category)}', '{EscapeSql(requiredMenu)}', {order}, '{EscapeSql(size)}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets} WHERE code = '{EscapeSql(code)}'
);
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets}
WHERE code IN ('{string.Join("','", DashboardWidgetCodes.All)}');
""");
    }

    private static string EscapeSql(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
