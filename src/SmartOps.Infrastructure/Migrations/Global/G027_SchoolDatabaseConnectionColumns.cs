using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(27, "Global — school database connection metadata")]
public sealed class G027_SchoolDatabaseConnectionColumns : Migration
{
    public override void Up()
    {
        string g = DatabaseConfig.Schema_Global;

        if (!Schema.Schema(g).Table(DatabaseConfig.TableSchools).Column("databasename").Exists())
        {
            Alter.Table(DatabaseConfig.TableSchools).InSchema(g)
                .AddColumn("databasename").AsString(128).Nullable();
        }

        if (!Schema.Schema(g).Table(DatabaseConfig.TableSchools).Column("connectionstring").Exists())
        {
            Alter.Table(DatabaseConfig.TableSchools).InSchema(g)
                .AddColumn("connectionstring").AsCustom("text").Nullable();
        }
    }

    public override void Down()
    {
        string g = DatabaseConfig.Schema_Global;

        if (Schema.Schema(g).Table(DatabaseConfig.TableSchools).Column("connectionstring").Exists())
        {
            Delete.Column("connectionstring").FromTable(DatabaseConfig.TableSchools).InSchema(g);
        }

        if (Schema.Schema(g).Table(DatabaseConfig.TableSchools).Column("databasename").Exists())
        {
            Delete.Column("databasename").FromTable(DatabaseConfig.TableSchools).InSchema(g);
        }
    }
}
