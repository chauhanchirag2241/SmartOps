using FluentMigrator;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(98, "School database — enable pgcrypto")]
public sealed class S098_EnablePgCrypto : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
    }

    public override void Down()
    {
        // Extension is shared; do not drop on migration rollback.
    }
}
