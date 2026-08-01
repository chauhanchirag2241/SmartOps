using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(115, "School template — salarystructure")]
public sealed class S115_CreateSalaryStructureTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string StructureBranchUnique = "uq_salarystructure_branch_version";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableSalaryStructure).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSalaryStructure).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey("pk_salarystructure").NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("branchid").AsGuid().NotNullable()
            .WithColumn("versionnumber").AsInt32().NotNullable()
            .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("effectivedate").AsDate().Nullable()
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableSalaryStructure}
    ADD CONSTRAINT fk_salarystructure_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_salarystructure_branchid ON {S}.{DatabaseConfig.TableSalaryStructure} (branchid);
""");

        Create.UniqueConstraint(StructureBranchUnique)
            .OnTable(DatabaseConfig.TableSalaryStructure).WithSchema(S)
            .Columns("branchid", "versionnumber");
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSalaryStructure).InSchema(S);
}
