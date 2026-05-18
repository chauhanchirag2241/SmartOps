using FluentMigrator;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(111, "School template — teacher assignment permission columns")]
public sealed class S111_AlterTeacherAssignmentPermissions : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Exists())
        {
            if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canviewstudents").Exists())
            {
                Alter.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                    .AddColumn("canviewstudents").AsBoolean().NotNullable().WithDefaultValue(true);
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canmarkattendance").Exists())
            {
                Alter.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                    .AddColumn("canmarkattendance").AsBoolean().NotNullable().WithDefaultValue(false);
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canaddmarks").Exists())
            {
                Alter.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                    .AddColumn("canaddmarks").AsBoolean().NotNullable().WithDefaultValue(false);
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("cansendnotice").Exists())
            {
                Alter.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                    .AddColumn("cansendnotice").AsBoolean().NotNullable().WithDefaultValue(false);
            }
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherSubjectAssignments).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableTeacherSubjectAssignments).Column("canaddmarks").Exists())
        {
            Alter.Table(DatabaseConfig.TableTeacherSubjectAssignments).InSchema(S)
                .AddColumn("canaddmarks").AsBoolean().NotNullable().WithDefaultValue(true);
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("cansendnotice").Exists())
        {
            Delete.Column("cansendnotice").FromTable(DatabaseConfig.TableTeacherClassAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canaddmarks").Exists())
        {
            Delete.Column("canaddmarks").FromTable(DatabaseConfig.TableTeacherClassAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canmarkattendance").Exists())
        {
            Delete.Column("canmarkattendance").FromTable(DatabaseConfig.TableTeacherClassAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Column("canviewstudents").Exists())
        {
            Delete.Column("canviewstudents").FromTable(DatabaseConfig.TableTeacherClassAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeacherSubjectAssignments).Column("canaddmarks").Exists())
        {
            Delete.Column("canaddmarks").FromTable(DatabaseConfig.TableTeacherSubjectAssignments).InSchema(S);
        }
    }
}
