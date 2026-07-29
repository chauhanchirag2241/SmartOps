using FluentMigrator;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(23, "Global — usertypes already seeded in G001 (noop)")]
public sealed class G023_SeedParentAndStudentUserTypes : Migration
{
    public override void Up()
    {
        // Canonical user types (STUDENT, TEACHER, ACCOUNTANT, NON_ACADEMIC_STAFF, OFFICE_STAFF)
        // are created in G001. Parent user type is intentionally not seeded.
    }

    public override void Down()
    {
    }
}
