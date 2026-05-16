using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(106, "School template — settings")]
public sealed class S106_CreateSettingsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableSettings).Exists())
        {
            Create.Table(DatabaseConfig.TableSettings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("key").AsString(100).NotNullable().Unique()
                .WithColumn("value").AsString(500).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithAuditColumns();

            Insert.IntoTable(DatabaseConfig.TableSettings).InSchema(S).Row(new
            {
                key = "Student_AdmissionNo_Sequence",
                value = "1",
                description = "Next sequence number for student admission"
            });
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSettings).InSchema(S);
}
