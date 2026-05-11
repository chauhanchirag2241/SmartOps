//using FluentMigrator;
//using SmartOps.Shared.Configuration;
//using SmartOps.Shared.Constants;

//namespace SmartOps.Infrastructure.Migrations;

//[Migration(009)]
//public sealed class M009_SeedPermissions : Migration
//{
//    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

//    public override void Up()
//    {
//        DateTimeOffset now = DateTimeOffset.UtcNow;

//        string[] permissionNames = new[]
//        {
//            PermissionNames.StudentRead,
//            PermissionNames.StudentCreate,
//            PermissionNames.StudentUpdate,
//            PermissionNames.StudentDelete,
//            PermissionNames.AttendanceRead,
//            PermissionNames.AttendanceMark,
//            PermissionNames.FeesRead,
//            PermissionNames.FeesCreate,
//            PermissionNames.FeesUpdate,
//            PermissionNames.ExamsRead,
//            PermissionNames.ExamsCreate,
//            PermissionNames.HrRead,
//            PermissionNames.HrManage,
//            PermissionNames.ReportsView,
//            PermissionNames.AdminFull
//        };

//        foreach (string name in permissionNames)
//        {
//            Insert.IntoTable(DatabaseConfig.TablePermissions).InSchema(DatabaseConfig.Schema_Global)
//                .Row(new
//                {
//                    id = Guid.NewGuid(),
//                    name,
//                    description = (string?)null,
//                    isactive = true,
//                    versionno = 1,
//                    createdby = SeedActor,
//                    createdon = now,
//                    updatedby = SeedActor,
//                    updatedon = now
//                });
//        }
//    }

//    public override void Down()
//    {
//        Execute.Sql(
//            $"""
//            DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions}
//            WHERE name IN (
//                'student.read','student.create','student.update','student.delete',
//                'attendance.read','attendance.mark',
//                'fees.read','fees.create','fees.update',
//                'exams.read','exams.create',
//                'hr.read','hr.manage',
//                'reports.view','admin.full'
//            );
//            """);
//    }
//}
