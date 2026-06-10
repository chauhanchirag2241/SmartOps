using FluentMigrator;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(0, "Global — enable pgcrypto")]
public sealed class G000_EnablePgCrypto : Migration
{
    public override void Up() => Execute.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

    public override void Down() => Execute.Sql("DROP EXTENSION IF EXISTS pgcrypto;");
}
