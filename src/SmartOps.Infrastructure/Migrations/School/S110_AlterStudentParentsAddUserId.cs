using FluentMigrator;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(110, "School template — studentparents userid and email")]
public sealed class S110_AlterStudentParentsAddUserId : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentParents).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableStudentParents).Column("userid").Exists())
        {
            Alter.Table(DatabaseConfig.TableStudentParents).InSchema(S)
                .AddColumn("userid").AsGuid().Nullable()
                .AddColumn("email").AsString(256).Nullable();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableStudentParents}
    ADD CONSTRAINT fk_studentparents_userid FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE SET NULL;
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentParents).Column("userid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableStudentParents} DROP CONSTRAINT IF EXISTS fk_studentparents_userid;");
            Delete.Column("email").FromTable(DatabaseConfig.TableStudentParents).InSchema(S);
            Delete.Column("userid").FromTable(DatabaseConfig.TableStudentParents).InSchema(S);
        }
    }
}
