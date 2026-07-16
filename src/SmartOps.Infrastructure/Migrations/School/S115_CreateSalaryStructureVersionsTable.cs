using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(115, "School template — salarystructureversions")]
public sealed class S115_CreateSalaryStructureVersionsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string VersionYearUnique = "uq_salarystructureversions_year_version";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableSalaryStructureVersions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSalaryStructureVersions).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("branchid").AsGuid().NotNullable()
            .WithColumn("academicyearid").AsGuid().NotNullable()
            .WithColumn("versionnumber").AsInt32().NotNullable()
            .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("effectivedate").AsDate().Nullable()
            .WithColumn("publishedon").AsDateTime().Nullable()
            .WithColumn("activatedon").AsDateTime().Nullable()
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableSalaryStructureVersions}
    ADD CONSTRAINT fk_salarystructureversions_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_salarystructureversions_branchid ON {S}.{DatabaseConfig.TableSalaryStructureVersions} (branchid);
""");

        Create.UniqueConstraint(VersionYearUnique)
            .OnTable(DatabaseConfig.TableSalaryStructureVersions).WithSchema(S)
            .Columns("branchid", "academicyearid", "versionnumber");
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSalaryStructureVersions).InSchema(S);
}
