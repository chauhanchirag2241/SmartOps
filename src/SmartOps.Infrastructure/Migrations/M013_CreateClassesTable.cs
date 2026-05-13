using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(013)]
public sealed class M013_CreateClassesTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableClasses).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classname").AsString(50).NotNullable()
                .WithColumn("section").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("streamgroup").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("academicyear").AsString(50).NotNullable()
                .WithColumn("capacity").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("classteacher").AsString(200).Nullable()  //change when add teacher screen
                .WithColumn("roomnumber").AsString(50).Nullable()
                .WithColumn("shift").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("medium").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("description").AsString(1000).Nullable()
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableClasses).InSchema(DatabaseConfig.Schema_Global);
    }
}
