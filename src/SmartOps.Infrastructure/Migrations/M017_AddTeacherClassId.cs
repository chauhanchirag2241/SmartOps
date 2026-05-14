using FluentMigrator;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(017)]
public sealed class M017_AddTeacherClassId : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableTeachers).Column("classid").Exists())
        {
            Alter.Table(DatabaseConfig.TableTeachers).InSchema(DatabaseConfig.Schema_Global)
                .AddColumn("classid").AsGuid().Nullable()
                .ForeignKey("fk_teachers_classid", DatabaseConfig.Schema_Global, DatabaseConfig.TableClasses, "id");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableTeachers).Column("classid").Exists())
        {
            Delete.ForeignKey("fk_teachers_classid").OnTable(DatabaseConfig.TableTeachers).InSchema(DatabaseConfig.Schema_Global);
            Delete.Column("classid").FromTable(DatabaseConfig.TableTeachers).InSchema(DatabaseConfig.Schema_Global);
        }
    }
}
