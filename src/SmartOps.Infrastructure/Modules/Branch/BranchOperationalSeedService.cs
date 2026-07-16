using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Infrastructure.Modules.Branch;

/// <summary>
/// Seeds per-branch operational defaults (departments, front-office lookups) into a school database.
/// </summary>
public sealed class BranchOperationalSeedService
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (string Code, string Name)[] DefaultDepartments =
    [
        ("ACADEMICS", "Academics"),
        ("ACCOUNTS", "Accounts"),
        ("ADMIN", "Administration"),
        ("HR", "Human Resources"),
        ("NON_ACADEMIC_STAFF", "Non Academic Staff"),
    ];

    private static readonly (string Name, string Desc, int Order)[] ComplaintTypes =
    [
        ("Infrastructure Issue", "Building, furniture, water, electricity problems", 1),
        ("Staff Behavior", "Complaints regarding teacher or staff conduct", 2),
        ("Academic Issue", "Curriculum, homework, exam-related complaints", 3),
        ("Fee Related", "Fee overcharge, receipt issues, payment disputes", 4),
        ("Transport Issue", "Bus timing, driver behavior, route problems", 5),
        ("Cleanliness / Hygiene", "Washroom, canteen, campus cleanliness", 6),
    ];

    private static readonly (string Name, string Desc, int Order)[] VisitorPurposes =
    [
        ("Meeting with Teacher", "Parent meeting with class or subject teacher", 1),
        ("Fee Payment", "Paying school fees or getting receipt", 2),
        ("Document Submission", "Submitting required documents", 3),
        ("Admission Inquiry", "Inquiring about admission for new student", 4),
        ("Collect TC/Certificate", "Collecting transfer certificate or other docs", 5),
        ("Official Visit", "Government official, inspector or auditor", 6),
        ("Personal Visit", "Personal or unofficial visit", 7),
    ];

    private readonly IDbConnectionFactory _connectionFactory;

    public BranchOperationalSeedService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SeedForSchoolAsync(
        SchoolEntity school,
        CancellationToken cancellationToken = default)
    {
        if (school.Branches.Count == 0)
        {
            return;
        }

        string? connectionString = school.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Per-school DB mode: departments/front-office live in the school database only.
            // Never fall back to platform — that causes "relation school.departments does not exist".
            return;
        }

        using var connection = await _connectionFactory
            .CreateConnectionAsync(connectionString, cancellationToken)
            .ConfigureAwait(false);

        string s = DatabaseConfig.Schema_School;
        string g = DatabaseConfig.Schema_Global;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (SchoolBranchEntity branch in school.Branches)
        {
            await connection.ExecuteAsync(
                $"""
INSERT INTO {g}.{DatabaseConfig.TableSchoolBranches}
    (id, schoolid, name, email, address, isheadoffice, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @Id, @SchoolId, @Name, @Email, @Address, @IsHeadOffice, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (SELECT 1 FROM {g}.{DatabaseConfig.TableSchoolBranches} WHERE id = @Id);
""",
                new
                {
                    branch.Id,
                    SchoolId = school.Id,
                    branch.Name,
                    branch.Email,
                    branch.Address,
                    branch.IsHeadOffice,
                    Actor = SeedActor,
                    Now = now
                }).ConfigureAwait(false);

            foreach ((string code, string name) in DefaultDepartments)
            {
                await connection.ExecuteAsync(
                    $"""
INSERT INTO {s}.{DatabaseConfig.TableDepartments}
    (id, branchid, code, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @BranchId, @Code, @Name, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {s}.{DatabaseConfig.TableDepartments}
    WHERE branchid = @BranchId AND lower(code) = lower(@Code) AND isactive = true
);
""",
                    new { BranchId = branch.Id, Code = code, Name = name, Actor = SeedActor, Now = now }).ConfigureAwait(false);
            }

            foreach ((string name, string desc, int order) in ComplaintTypes)
            {
                await connection.ExecuteAsync(
                    $"""
INSERT INTO {s}.{DatabaseConfig.TableComplaintTypes}
    (id, branchid, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @BranchId, @Name, @Desc, @Order, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {s}.{DatabaseConfig.TableComplaintTypes}
    WHERE branchid = @BranchId AND lower(name) = lower(@Name) AND isactive = true
);
""",
                    new { BranchId = branch.Id, Name = name, Desc = desc, Order = order, Actor = SeedActor, Now = now }).ConfigureAwait(false);
            }

            foreach ((string name, string desc, int order) in VisitorPurposes)
            {
                await connection.ExecuteAsync(
                    $"""
INSERT INTO {s}.{DatabaseConfig.TableVisitorPurposes}
    (id, branchid, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @BranchId, @Name, @Desc, @Order, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {s}.{DatabaseConfig.TableVisitorPurposes}
    WHERE branchid = @BranchId AND lower(name) = lower(@Name) AND isactive = true
);
""",
                    new { BranchId = branch.Id, Name = name, Desc = desc, Order = order, Actor = SeedActor, Now = now }).ConfigureAwait(false);
            }
        }
    }
}
