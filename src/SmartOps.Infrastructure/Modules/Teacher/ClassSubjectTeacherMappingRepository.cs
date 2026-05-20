using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Teacher;

public sealed class ClassSubjectTeacherMappingRepository : BaseRepository, IClassSubjectTeacherMappingRepository
{
    private const string SectionLabelSql = """
CASE c.section
    WHEN 1 THEN 'A'
    WHEN 2 THEN 'B'
    WHEN 3 THEN 'C'
    WHEN 4 THEN 'D'
    ELSE ''
END
""";

    private readonly DapperContext _context;

    public ClassSubjectTeacherMappingRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
        _context = context;
    }

    private string Schema => _context.OperationalSchema;

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByTeacherIdAsync(
        Guid teacherId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectSql("""
            m.teacherid = @TeacherId
            AND m.isactive = true
            AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
            """);

        return await QueryMappingsAsync(sql, new { TeacherId = teacherId, AcademicYearId = academicYearId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectSql("""
            m.classid = @ClassId
            AND m.isactive = true
            AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
            """);

        return await QueryMappingsAsync(sql, new { ClassId = classId, AcademicYearId = academicYearId }, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id AS Id, classid AS ClassId, subjectid AS SubjectId, teacherid AS TeacherId,
       academicyearid AS AcademicYearId, isclassteacher AS IsClassTeacher,
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

    public async Task<ClassSubjectTeacherMappingEntity?> FindByClassSubjectYearAsync(
        Guid classId,
        Guid subjectId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT id AS Id, classid AS ClassId, subjectid AS SubjectId, teacherid AS TeacherId,
       academicyearid AS AcademicYearId, isclassteacher AS IsClassTeacher,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
WHERE classid = @ClassId AND subjectid = @SubjectId AND academicyearid = @AcademicYearId
ORDER BY isactive DESC, updatedon DESC
LIMIT 1
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ClassSubjectTeacherMappingEntity>(
            new CommandDefinition(
                sql,
                new { ClassId = classId, SubjectId = subjectId, AcademicYearId = academicYearId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectSql("m.id = @Id AND m.isactive = true");
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await QueryMappingsAsync(
            sql,
            new { Id = id },
            cancellationToken).ConfigureAwait(false);

        return rows.FirstOrDefault();
    }

    public async Task<Guid?> GetClassAcademicYearIdAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT academicyearid FROM {Schema}.{DatabaseConfig.TableClasses}
WHERE id = @ClassId AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> ExistsActiveClassSubjectAsync(
        Guid classId,
        Guid subjectId,
        Guid academicYearId,
        Guid? excludeMappingId = null,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT EXISTS (
    SELECT 1 FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
    WHERE classid = @ClassId
      AND subjectid = @SubjectId
      AND academicyearid = @AcademicYearId
      AND isactive = true
      AND (@ExcludeMappingId IS NULL OR id <> @ExcludeMappingId)
)
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { ClassId = classId, SubjectId = subjectId, AcademicYearId = academicYearId, ExcludeMappingId = excludeMappingId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<Guid> InsertAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        DateTime utcNow = DateTime.UtcNow;
        EnsureInsertAudit(entity, utcNow, ResolveUpdateActor());

        string sql = $"""
INSERT INTO {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
    (id, classid, subjectid, teacherid, academicyearid, isclassteacher,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @ClassId, @SubjectId, @TeacherId, @AcademicYearId, @IsClassTeacher,
     true, 1, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
RETURNING id
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);

        string sql = $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET teacherid = @TeacherId,
    isclassteacher = @IsClassTeacher,
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

    public async Task<bool> SetClassTeacherFlagAsync(
        Guid mappingId,
        Guid classId,
        Guid academicYearId,
        bool isClassTeacher,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (isClassTeacher)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isclassteacher = false, updatedon = NOW(), versionno = versionno + 1
WHERE classid = @ClassId AND academicyearid = @AcademicYearId AND isactive = true
""",
                    new { ClassId = classId, AcademicYearId = academicYearId },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        Guid actorId = ResolveUpdateActor();
        DateTime utcNow = DateTime.UtcNow;

        int affected = await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isclassteacher = @IsClassTeacher,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @MappingId AND isactive = true
""",
                new
                {
                    MappingId = mappingId,
                    IsClassTeacher = isClassTeacher,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return affected > 0;
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

    public async Task ClearClassTeacherFlagAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isclassteacher = false, updatedon = NOW(), versionno = versionno + 1
WHERE classid = @ClassId AND academicyearid = @AcademicYearId AND isactive = true
""",
                new { ClassId = classId, AcademicYearId = academicYearId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetClassIdsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT DISTINCT m.classid
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {BuildTeacherUserMatchSql()}
  AND m.teacherid IS NOT NULL
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
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
INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {BuildTeacherUserMatchSql()}
  AND m.teacherid IS NOT NULL
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
SELECT m.classid AS ClassId, m.subjectid AS SubjectId
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {BuildTeacherUserMatchSql()}
  AND m.teacherid IS NOT NULL
  AND m.isactive = true
  AND t.isactive = true
  AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<PairRow> rows = await connection.QueryAsync<PairRow>(
            new CommandDefinition(sql, new { UserId = userId, AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(r => (r.ClassId, r.SubjectId)).ToList();
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
WHERE m.classid = ANY(@ClassIds)
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
        string sql = $"""
SELECT
    c.id AS ClassId,
    c.classname AS ClassName,
    {SectionLabelSql} AS Section,
    COUNT(m.id) FILTER (WHERE m.isactive = true) AS SubjectCount,
    COUNT(m.id) FILTER (WHERE m.isactive = true AND m.teacherid IS NOT NULL) AS TeachersAssignedCount,
    COUNT(m.id) FILTER (WHERE m.isactive = true AND m.isclassteacher = true) AS ClassTeacherCount
FROM {Schema}.{DatabaseConfig.TableClasses} c
LEFT JOIN {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
    ON m.classid = c.id
    AND m.isactive = true
    AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
WHERE c.isactive = true
  AND (@AcademicYearId IS NULL OR c.academicyearid = @AcademicYearId)
GROUP BY c.id, c.classname, c.section
ORDER BY c.classname, c.section
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ClassMappingSummaryDto> rows = await connection.QueryAsync<ClassMappingSummaryDto>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    private static string BuildTeacherUserMatchSql() =>
        $"""
(t.userid = @UserId OR EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} u
    WHERE u.id = @UserId AND u.isactive = true AND lower(trim(t.email)) = lower(trim(u.email))
))
""";

    private string BuildSelectSql(string whereClause) => $"""
SELECT
    m.id AS Id,
    m.classid AS ClassId,
    c.classname AS ClassName,
    m.subjectid AS SubjectId,
    s.subjectname AS SubjectName,
    s.subjectcode AS SubjectCode,
    m.teacherid AS TeacherId,
    CASE WHEN m.teacherid IS NULL THEN NULL ELSE trim(t.firstname || ' ' || t.lastname) END AS TeacherName,
    m.academicyearid AS AcademicYearId,
    m.isclassteacher AS IsClassTeacher
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = m.classid
INNER JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = m.subjectid
LEFT JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {whereClause}
ORDER BY s.subjectname
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
