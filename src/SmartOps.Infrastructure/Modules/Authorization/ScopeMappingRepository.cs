using Dapper;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Authorization;

public sealed class ScopeMappingRepository : IScopeMappingRepository
{
    private readonly DapperContext _context;

    public ScopeMappingRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetActiveAcademicYearIdAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schema) || schema == DatabaseConfig.Schema_Global)
        {
            return null;
        }

        string sql = $"""
SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
WHERE iscurrent = true AND isactive = true
LIMIT 1
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task EnsureTeacherLinkedToUserAsync(
        string schema,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
UPDATE {schema}.{DatabaseConfig.TableTeachers} t
SET userid = @UserId,
    updatedon = NOW(),
    versionno = t.versionno + 1
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} u
WHERE u.id = @UserId
  AND u.isactive = true
  AND t.isactive = true
  AND t.userid IS NULL
  AND lower(trim(t.email)) = lower(trim(u.email))
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetTeacherClassIdsAsync(
        string schema,
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string teacherMatch = BuildTeacherUserMatchSql();
        string sql = $"""
SELECT DISTINCT m.classid
FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {teacherMatch}
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";
        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetTeacherSubjectIdsAsync(
        string schema,
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string teacherMatch = BuildTeacherUserMatchSql();
        string sql = $"""
SELECT DISTINCT m.subjectid
FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {teacherMatch}
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";
        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildTeacherUserMatchSql() =>
        $"""
(t.userid = @UserId OR EXISTS (
    SELECT 1
    FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} u
    WHERE u.id = @UserId
      AND u.isactive = true
      AND lower(trim(u.email)) = lower(trim(t.email))
))
""";

    public async Task<IReadOnlyList<Guid>> GetDepartmentIdsForHodAsync(
        string schema,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT departmentid FROM {schema}.{DatabaseConfig.TableHodDepartmentAssignments}
WHERE userid = @UserId AND isactive = true
""";
        return await QueryGuidListAsync(sql, new { UserId = userId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetClassIdsByDepartmentsAsync(
        string schema,
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken = default)
    {
        if (departmentIds.Count == 0)
        {
            return [];
        }

        string sql = $"""
SELECT DISTINCT m.classid
FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE t.departmentid = ANY(@DepartmentIds)
  AND m.isactive = true
  AND t.isactive = true
""";
        return await QueryGuidListAsync(sql, new { DepartmentIds = departmentIds.ToArray() }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetTeacherIdsByDepartmentsAsync(
        string schema,
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken = default)
    {
        if (departmentIds.Count == 0)
        {
            return [];
        }

        string sql = $"""
SELECT id FROM {schema}.{DatabaseConfig.TableTeachers}
WHERE departmentid = ANY(@DepartmentIds) AND isactive = true
""";
        return await QueryGuidListAsync(sql, new { DepartmentIds = departmentIds.ToArray() }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetStudentIdsByClassIdsAsync(
        string schema,
        IReadOnlyList<Guid> classIds,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        if (classIds.Count == 0)
        {
            return [];
        }

        string sql = $"""
SELECT DISTINCT sa.studentid
FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
WHERE sa.classid = ANY(@ClassIds) AND sa.isactive = true
  AND (@AcademicYearId IS NULL OR sa.academicyearid = @AcademicYearId)
""";
        return await QueryGuidListAsync(sql, new { ClassIds = classIds.ToArray(), AcademicYearId = academicYearId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid?> GetStudentIdByUserIdAsync(
        string schema,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id FROM {schema}.{DatabaseConfig.TableStudents}
WHERE userid = @UserId AND isactive = true
LIMIT 1
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetLinkedStudentIdsForParentAsync(
        string schema,
        Guid parentUserId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT studentid FROM {schema}.{DatabaseConfig.TableParentStudentMappings}
WHERE parentuserid = @UserId AND isactive = true
""";
        return await QueryGuidListAsync(sql, new { UserId = parentUserId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetStaffScopeClassIdsAsync(
        string schema,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT scopevalue FROM {schema}.{DatabaseConfig.TableStaffScopeAssignments}
WHERE userid = @UserId AND scopetype = 'Class' AND isactive = true
""";
        return await QueryGuidListAsync(sql, new { UserId = userId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetStaffScopeDepartmentIdsAsync(
        string schema,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT scopevalue FROM {schema}.{DatabaseConfig.TableStaffScopeAssignments}
WHERE userid = @UserId AND scopetype = 'Department' AND isactive = true
""";
        return await QueryGuidListAsync(sql, new { UserId = userId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertParentStudentMappingAsync(
        string schema,
        Guid parentUserId,
        Guid studentId,
        string relationType,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableParentStudentMappings}
    (id, parentuserid, studentid, relationtype, isprimary, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES (gen_random_uuid(), @ParentUserId, @StudentId, @RelationType, true, true, 1,
        '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW())
ON CONFLICT ON CONSTRAINT uq_parentstudentmappings DO UPDATE SET
    relationtype = EXCLUDED.relationtype,
    isactive = true,
    updatedon = NOW()
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { ParentUserId = parentUserId, StudentId = studentId, RelationType = relationType }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpsertHodDepartmentAssignmentAsync(
        string schema,
        Guid userId,
        Guid departmentId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableHodDepartmentAssignments}
    (id, userid, departmentid, academicyearid, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES (gen_random_uuid(), @UserId, @DepartmentId, @AcademicYearId, true, 1,
        '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW())
ON CONFLICT ON CONSTRAINT uq_hoddepartmentassignments DO UPDATE SET
    academicyearid = EXCLUDED.academicyearid,
    isactive = true,
    updatedon = NOW()
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, DepartmentId = departmentId, AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
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
}
