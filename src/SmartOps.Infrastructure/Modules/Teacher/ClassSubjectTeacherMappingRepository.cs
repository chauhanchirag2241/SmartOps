using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Teacher;

public sealed class ClassSubjectTeacherMappingRepository : BaseRepository, IClassSubjectTeacherMappingRepository
{
    private readonly DapperContext _context;
    private readonly IBranchContext _branchContext;

    public ClassSubjectTeacherMappingRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext)
        : base(context, currentUser)
    {
        _context = context;
        _branchContext = branchContext;
    }

    private string Schema => _context.OperationalSchema;

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByEmployeeIdAsync(
        Guid employeeId,
        Guid? academicYearId,
        bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        string activeFilter = includeInactive ? string.Empty : "AND m.isactive = true";
        string sql = BuildSelectSql($"""
            m.employeeid = @EmployeeId
            {activeFilter}
            AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
            """);

        return await QueryMappingsAsync(sql, new { EmployeeId = employeeId, AcademicYearId = academicYearId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        // Callers may pass class group id; filter by classgroupid. Scope/list for class uses active only.
        string sql = BuildSelectSql("""
            m.classgroupid = @ClassGroupId
            AND m.isactive = true
            AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
            """);

        return await QueryMappingsAsync(
                sql,
                new { ClassGroupId = classId, AcademicYearId = academicYearId },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id AS Id, classgroupid AS ClassGroupId, subjectid AS SubjectId, employeeid AS EmployeeId,
       academicyearid AS AcademicYearId,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
WHERE id = @Id
LIMIT 1
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ClassSubjectTeacherMappingEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingEntity?> FindByClassGroupSubjectEmployeeYearAsync(
        Guid classGroupId,
        Guid subjectId,
        Guid employeeId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id AS Id, classgroupid AS ClassGroupId, subjectid AS SubjectId, employeeid AS EmployeeId,
       academicyearid AS AcademicYearId,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
WHERE classgroupid = @ClassGroupId
  AND subjectid = @SubjectId
  AND employeeid = @EmployeeId
  AND academicyearid = @AcademicYearId
ORDER BY isactive DESC, updatedon DESC
LIMIT 1
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ClassSubjectTeacherMappingEntity>(
            new CommandDefinition(
                sql,
                new
                {
                    ClassGroupId = classGroupId,
                    SubjectId = subjectId,
                    EmployeeId = employeeId,
                    AcademicYearId = academicYearId
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectSql("m.id = @Id");
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await QueryMappingsAsync(
            sql,
            new { Id = id },
            cancellationToken).ConfigureAwait(false);

        return rows.FirstOrDefault();
    }

    public async Task<bool> ExistsActiveClassGroupAsync(Guid classGroupId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT EXISTS (
    SELECT 1 FROM {Schema}.{DatabaseConfig.TableClassGroups} cg
    WHERE cg.id = @ClassGroupId AND cg.isactive = true)
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { ClassGroupId = classGroupId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> AllSubjectsBelongToClassGroupAsync(
        Guid classGroupId,
        IReadOnlyList<Guid> subjectIds,
        CancellationToken cancellationToken = default)
    {
        if (subjectIds.Count == 0)
        {
            return false;
        }

        Guid[] ids = subjectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return false;
        }

        string sql = $"""
SELECT COUNT(DISTINCT id) = @ExpectedCount
FROM {Schema}.{DatabaseConfig.TableSubjects}
WHERE classgroupid = @ClassGroupId
  AND id = ANY(@SubjectIds)
  AND isactive = true
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { ClassGroupId = classGroupId, SubjectIds = ids, ExpectedCount = ids.Length },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Guid> InsertAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        DateTime now = SchoolLocalTime.NowDateTime();
        EnsureInsertAudit(entity, now, ResolveUpdateActor());

        string sql = $"""
INSERT INTO {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
    (id, classgroupid, subjectid, employeeid, academicyearid,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @ClassGroupId, @SubjectId, @EmployeeId, @AcademicYearId,
     true, 1, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
RETURNING id
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default)
    {
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, now);

        string sql = $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET subjectid = @SubjectId,
    employeeid = @EmployeeId,
    isactive = @IsActive,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @Id
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        int affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (affected > 0)
        {
            entity.VersionNo++;
        }

        return affected;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isactive = false, updatedon = NOW(), versionno = versionno + 1
WHERE id = @Id
""",
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isactive = true, updatedon = NOW(), versionno = versionno + 1
WHERE id = @Id
""",
                new { Id = id },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetClassIdsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT DISTINCT classid FROM (
    SELECT c.id AS classid
    FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
    INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid
    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c
        ON c.classgroupid = m.classgroupid AND c.isactive = true
    WHERE {BuildEmployeeUserMatchSql()}
      AND m.isactive = true
      AND t.isactive = true
      AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
    UNION
    SELECT cs.sectionid AS classid
    FROM {Schema}.{DatabaseConfig.TableClassSettings} cs
    INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = cs.teacherid
    WHERE {BuildEmployeeUserMatchSql()}
      AND cs.isactive = true
      AND t.isactive = true
      AND cs.teacherid IS NOT NULL
      AND cs.sectionid IS NOT NULL
) scoped_classes
""";

        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetSubjectIdsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT DISTINCT m.subjectid
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid
WHERE {BuildEmployeeUserMatchSql()}
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";

        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(Guid ClassId, Guid SubjectId)>> GetClassSubjectPairsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT c.id AS ClassId, m.subjectid AS SubjectId
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid
INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c
    ON c.classgroupid = m.classgroupid AND c.isactive = true
WHERE {BuildEmployeeUserMatchSql()}
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<PairRow> rows = await connection.QueryAsync<PairRow>(
            new CommandDefinition(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(r => (r.ClassId, r.SubjectId)).Distinct().ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetSubjectIdsForClassIdsAsync(
        IReadOnlyList<Guid> classIds,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        if (classIds.Count == 0)
        {
            return [];
        }

        string sql = $"""
SELECT DISTINCT m.subjectid
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.classgroupid = m.classgroupid
WHERE c.id = ANY(@ClassIds)
  AND c.isactive = true
  AND m.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";

        return await QueryGuidListAsync(
            sql,
            new { ClassIds = classIds.ToArray(), AcademicYearId = academicYearId },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassMappingSummaryDto>> GetClassSummariesAsync(
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "cg", cancellationToken)
            .ConfigureAwait(false);

        string sql = $"""
SELECT
    c.id AS ClassId,
    cg.classname AS ClassName,
    c.section AS Section,
    COALESCE((
        SELECT COUNT(DISTINCT m2.subjectid)
        FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m2
        WHERE m2.classgroupid = c.classgroupid
          AND m2.isactive = true
          AND (@AcademicYearId IS NULL OR m2.academicyearid = @AcademicYearId)
    ), 0) AS SubjectCount,
    COUNT(DISTINCT m.employeeid) FILTER (WHERE m.isactive = true) AS EmployeesAssignedCount,
    CASE WHEN EXISTS (
        SELECT 1 FROM {Schema}.{DatabaseConfig.TableClassSettings} cs
        WHERE cs.sectionid = c.id AND cs.isactive = true AND cs.teacherid IS NOT NULL
    ) THEN 1 ELSE 0 END AS ClassTeacherCount
FROM {Schema}.{DatabaseConfig.TableClasses} c
INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
LEFT JOIN {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
    ON m.classgroupid = c.classgroupid
    AND m.isactive = true
    AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
WHERE c.isactive = true{branchFilter}
GROUP BY c.id, cg.classname, c.section, c.classgroupid
ORDER BY cg.classname, c.section
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ClassMappingSummaryDto> rows = await connection.QueryAsync<ClassMappingSummaryDto>(
            new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, ActiveBranchId = activeBranchId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    private static string BuildEmployeeUserMatchSql() =>
        "t.userid = @UserId";

    private string BuildSelectSql(string whereClause) => $"""
SELECT
    m.id AS Id,
    m.classgroupid AS ClassGroupId,
    cg.classname AS ClassGroupName,
    m.subjectid AS SubjectId,
    s.subjectname AS SubjectName,
    s.subjectcode AS SubjectCode,
    m.employeeid AS EmployeeId,
    CASE WHEN m.employeeid IS NULL THEN NULL ELSE trim(tu.firstname || ' ' || tu.lastname) END AS EmployeeName,
    m.academicyearid AS AcademicYearId,
    m.isactive AS IsActive
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = m.classgroupid
INNER JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = m.subjectid
LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid
LEFT JOIN {_context.IdentitySchema}.{DatabaseConfig.TableUsers} tu ON tu.id = t.userid
WHERE {whereClause}
ORDER BY cg.classname, s.subjectname, tu.firstname, tu.lastname
""";

    private async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> QueryMappingsAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ClassSubjectTeacherMappingDto> rows = await connection.QueryAsync<ClassSubjectTeacherMappingDto>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToList();
    }

    private async Task<IReadOnlyList<Guid>> QueryGuidListAsync(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Distinct().ToList();
    }

    private sealed class PairRow
    {
        public Guid ClassId { get; init; }

        public Guid SubjectId { get; init; }
    }
}
