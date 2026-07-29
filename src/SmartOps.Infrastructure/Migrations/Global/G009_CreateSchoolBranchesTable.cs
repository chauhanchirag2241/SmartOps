using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>
/// Formerly created platform schoolbranches / userbranchmappings.
/// Branches and mappings now live only on each school DB (<c>man</c> schema).
/// </summary>
[Tags("Global")]
[Migration(9, "Global — school branches retired (school DB man only)")]
public sealed class G009_CreateSchoolBranchesTable : Migration
{
    public override void Up()
    {
        // No-op: schoolbranches and userbranchmappings are created in S099 (man schema).
    }

    public override void Down()
    {
    }
}
