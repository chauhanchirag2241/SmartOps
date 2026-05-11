using FluentMigrator;

namespace SmartOps.Infrastructure.Migrations;

[Migration(0)]
public sealed class M000_EnablePgCrypto : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
    }

    public override void Down()
    {
        Execute.Sql("DROP EXTENSION IF EXISTS pgcrypto;");
    }
}