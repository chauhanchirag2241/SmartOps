using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(019)]
public sealed class M019_CreateSettingsTable : Migration
{
    public override void Up()
    {
        var schema = DatabaseConfig.Schema_Global;
        var table = "settings";

        if (!Schema.Schema(schema).Table(table).Exists())
        {
            Create.Table(table).InSchema(schema)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("key").AsString(100).NotNullable().Unique()
                .WithColumn("value").AsString(500).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithAuditColumns();

            // Seed initial admission number sequence
            Insert.IntoTable(table).InSchema(schema).Row(new
            {
                key = "Student_AdmissionNo_Sequence",
                value = "1",
                description = "Next sequence number for student admission"
            });
        }
    }

    public override void Down()
    {
        Delete.Table("settings").InSchema(DatabaseConfig.Schema_Global);
    }
}
