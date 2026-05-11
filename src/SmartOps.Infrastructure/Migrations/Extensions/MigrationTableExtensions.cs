using FluentMigrator;
using FluentMigrator.Builders.Create.Table;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Extensions;

public static class MigrationTableExtensions
{
    public static ICreateTableColumnOptionOrWithColumnSyntax WithAuditColumns(
        this ICreateTableColumnOptionOrWithColumnSyntax table)
    {
        return table
            .WithColumn("isactive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("versionno").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("createdby").AsGuid().NotNullable().WithDefaultValue(Guid.Parse(DatabaseConfig.SystemUserId))
            .WithColumn("createdon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("updatedby").AsGuid().NotNullable().WithDefaultValue(Guid.Parse(DatabaseConfig.SystemUserId))
            .WithColumn("updatedon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
    }
}
