using Dapper;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Authorization.Repositories;

public sealed class ScopeMappingRepository : IScopeMappingRepository
{
    private readonly DapperContext _context;

    public ScopeMappingRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetActiveAcademicYearIdAsync(string schema, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
WHERE isactive = true
ORDER BY startdate DESC NULLS LAST, createdon DESC
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
        string teacherMatch = BuildTeacherUserMatchSql(schema);
        string sql = $"""
SELECT DISTINCT x.classid
FROM (
    SELECT tca.classid
    FROM {schema}.{DatabaseConfig.TableTeacherClassAssignments} tca
    INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.id = tca.teacherid
    WHERE {teacherMatch}
      AND tca.isactive = true
      AND t.isactive = true
      AND (@AcademicYearId IS NULL OR tca.academicyearid = @AcademicYearId)
    UNION
    SELECT t.classid
    FROM {schema}.{DatabaseConfig.TableTeachers} t
    WHERE {teacherMatch}
      AND t.classid IS NOT NULL
      AND t.isactive = true
) x
WHERE x.classid IS NOT NULL
""";
        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetTeacherAttendanceClassIdsAsync(
        string schema,
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string teacherMatch = BuildTeacherUserMatchSql(schema);
        string sql = $"""
SELECT DISTINCT tca.classid
FROM {schema}.{DatabaseConfig.TableTeacherClassAssignments} tca
INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.id = tca.teacherid
WHERE {teacherMatch}
  AND tca.isactive = true
  AND t.isactive = true
  AND tca.canmarkattendance = true
  AND (@AcademicYearId IS NULL OR tca.academicyearid = @AcademicYearId)
""";
        return await QueryGuidListAsync(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildTeacherUserMatchSql(string schema) =>
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
SELECT DISTINCT c.id
FROM {schema}.{DatabaseConfig.TableClasses} c
INNER JOIN {schema}.{DatabaseConfig.TableTeachers} t ON t.classid = c.id
WHERE t.departmentid = ANY(@DepartmentIds) AND t.isactive = true AND c.isactive = true
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

    public async Task BackfillTeacherClassAssignmentsFromLegacyAsync(
        string schema,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        if (academicYearId is null)
        {
            return;
        }

        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableTeacherClassAssignments}
    (id, teacherid, classid, academicyearid, isclassteacher, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), t.id, t.classid, @AcademicYearId, true, true, 1,
       '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW()
FROM {schema}.{DatabaseConfig.TableTeachers} t
WHERE t.classid IS NOT NULL AND t.isactive = true
  AND NOT EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableTeacherClassAssignments} tca
    WHERE tca.teacherid = t.id AND tca.classid = t.classid AND tca.academicyearid = @AcademicYearId
  )
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpsertTeacherClassAssignmentAsync(
        string schema,
        Guid teacherId,
        Guid classId,
        Guid academicYearId,
        bool isClassTeacher,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableTeacherClassAssignments}
    (id, teacherid, classid, academicyearid, isclassteacher, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES (gen_random_uuid(), @TeacherId, @ClassId, @AcademicYearId, @IsClassTeacher, true, 1,
        '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW())
ON CONFLICT ON CONSTRAINT uq_teacherclassassignments DO UPDATE SET
    isclassteacher = EXCLUDED.isclassteacher,
    isactive = true,
    updatedon = NOW(),
    versionno = versionno + 1
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { TeacherId = teacherId, ClassId = classId, AcademicYearId = academicYearId, IsClassTeacher = isClassTeacher }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
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
