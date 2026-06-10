using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(101, "School template — classes")]
public sealed class S101_CreateClassesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableClasses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classname").AsString(50).NotNullable()
                .WithColumn("section").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("streamgroup").AsInt32().Nullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_classes_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("capacity").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("roomnumber").AsString(50).Nullable()
                .WithColumn("shift").AsInt32().Nullable()
                .WithColumn("medium").AsInt32().Nullable()
                .WithColumn("description").AsString(1000).Nullable()
                .WithAuditColumns();

            Create.UniqueConstraint("uq_classes_identity")
                .OnTable(DatabaseConfig.TableClasses).WithSchema(S)
                .Columns("classname", "section", "streamgroup", "academicyearid");
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableClasses).InSchema(S);
}
