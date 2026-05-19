using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Teacher.Repositories;

public sealed class ClassSubjectTeacherMappingRepository : BaseRepository, IClassSubjectTeacherMappingRepository
{
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

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetBySubjectIdAsync(
        Guid subjectId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectSql("""
            m.subjectid = @SubjectId
            AND m.isactive = true
            AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
            """);

        return await QueryMappingsAsync(sql, new { SubjectId = subjectId, AcademicYearId = academicYearId }, cancellationToken)
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

    public async Task UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);

        string sql = $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isclassteacher = @IsClassTeacher,
    isactive = @IsActive,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @Id AND versionno = @VersionNo
""";

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, cancellationToken: cancellationToken)).ConfigureAwait(false);
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

    public async Task SoftDeleteByClassAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        await SoftDeleteWhereAsync(
            "classid = @ClassId AND academicyearid = @AcademicYearId",
            new { ClassId = classId, AcademicYearId = academicYearId },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteBySubjectAsync(Guid subjectId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        await SoftDeleteWhereAsync(
            "subjectid = @SubjectId AND academicyearid = @AcademicYearId",
            new { SubjectId = subjectId, AcademicYearId = academicYearId },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftDeleteByTeacherAsync(Guid teacherId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        await SoftDeleteWhereAsync(
            "teacherid = @TeacherId AND academicyearid = @AcademicYearId",
            new { TeacherId = teacherId, AcademicYearId = academicYearId },
            cancellationToken).ConfigureAwait(false);
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
    m.teacherid AS TeacherId,
    trim(t.firstname || ' ' || t.lastname) AS TeacherName,
    m.academicyearid AS AcademicYearId,
    m.isclassteacher AS IsClassTeacher
FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = m.classid
INNER JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = m.subjectid
INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = m.teacherid
WHERE {whereClause}
ORDER BY c.classname, s.subjectname, t.firstname
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

    private async Task SoftDeleteWhereAsync(string whereClause, object parameters, CancellationToken cancellationToken)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings}
SET isactive = false, updatedon = NOW(), versionno = versionno + 1
WHERE {whereClause} AND isactive = true
""",
                parameters,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
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
