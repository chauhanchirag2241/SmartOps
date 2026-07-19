using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(6, "Global — schools registry")]
public sealed class G006_CreateSchoolsTable : Migration
{
    public override void Up()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchools).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("subdomain").AsString(100).NotNullable().Unique()
            .WithColumn("schoolcode").AsString(50).Nullable()
            .WithColumn("registrationnumber").AsString(100).Nullable()
            .WithColumn("affiliatedboard").AsString(50).Nullable()
            .WithColumn("schooltype").AsString(50).Nullable()
            .WithColumn("establishedyear").AsInt32().Nullable()
            .WithColumn("aboutschool").AsString(int.MaxValue).Nullable()
            .WithColumn("streetaddress").AsString(500).Nullable()
            .WithColumn("city").AsString(100).Nullable()
            .WithColumn("state").AsString(100).Nullable()
            .WithColumn("pincode").AsString(10).Nullable()
            .WithColumn("country").AsString(100).Nullable().WithDefaultValue("India")
            .WithColumn("timezone").AsString(50).Nullable()
            .WithColumn("googlemapslink").AsString(500).Nullable()
            .WithColumn("latitude").AsDecimal(10, 6).Nullable()
            .WithColumn("longitude").AsDecimal(10, 6).Nullable()
            .WithColumn("primaryphone").AsString(20).Nullable()
            .WithColumn("alternatephone").AsString(20).Nullable()
            .WithColumn("fax").AsString(20).Nullable()
            .WithColumn("primaryemail").AsString(256).Nullable()
            .WithColumn("principalemail").AsString(256).Nullable()
            .WithColumn("website").AsString(500).Nullable()
            .WithColumn("schemaname").AsString(100).Nullable()
            .WithAuditColumns();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global);
}
