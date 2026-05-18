using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Teacher.Services;

public sealed class TeacherAssignmentService : ITeacherAssignmentService
{
    private readonly DapperContext _context;
    private readonly IScopeMappingRepository _scopeMapping;
    private readonly IUserScopeService _userScopeService;
    private readonly ITenantProvider _tenantProvider;

    public TeacherAssignmentService(
        DapperContext context,
        IScopeMappingRepository scopeMapping,
        IUserScopeService userScopeService,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _scopeMapping = scopeMapping;
        _userScopeService = userScopeService;
        _tenantProvider = tenantProvider;
    }

    public async Task<TeacherAssignmentsResponseDto> GetAssignmentsAsync(
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        string schema = _context.OperationalSchema;
        Guid? academicYearId = await _scopeMapping
            .GetActiveAcademicYearIdAsync(schema, cancellationToken)
            .ConfigureAwait(false);

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string classSql = $"""
SELECT
    tca.classid AS ClassId,
    tca.isclassteacher AS IsClassTeacher,
    tca.canviewstudents AS CanViewStudents,
    tca.canmarkattendance AS CanMarkAttendance,
    tca.canaddmarks AS CanAddMarks,
    tca.cansendnotice AS CanSendNotice
FROM {schema}.{DatabaseConfig.TableTeacherClassAssignments} tca
WHERE tca.teacherid = @TeacherId
  AND tca.isactive = true
  AND (@AcademicYearId IS NULL OR tca.academicyearid = @AcademicYearId)
ORDER BY tca.createdon
""";

        IEnumerable<ClassAssignmentRow> classRows = await connection.QueryAsync<ClassAssignmentRow>(
            new CommandDefinition(classSql, new { TeacherId = teacherId, AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        string subjectSql = $"""
SELECT tsa.classid AS ClassId, tsa.subjectid AS SubjectId
FROM {schema}.{DatabaseConfig.TableTeacherSubjectAssignments} tsa
WHERE tsa.teacherid = @TeacherId
  AND tsa.isactive = true
  AND (@AcademicYearId IS NULL OR tsa.academicyearid = @AcademicYearId)
""";

        IEnumerable<SubjectAssignmentRow> subjectRows = await connection.QueryAsync<SubjectAssignmentRow>(
            new CommandDefinition(subjectSql, new { TeacherId = teacherId, AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        Dictionary<Guid, List<Guid>> subjectsByClass = subjectRows
            .GroupBy(s => s.ClassId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SubjectId).Distinct().ToList());

        List<TeacherClassAssignmentRowDto> assignments = classRows.Select(row => new TeacherClassAssignmentRowDto
        {
            ClassId = row.ClassId,
            IsClassTeacher = row.IsClassTeacher,
            CanViewStudents = row.CanViewStudents,
            CanMarkAttendance = row.CanMarkAttendance,
            CanAddMarks = row.CanAddMarks,
            CanSendNotice = row.CanSendNotice,
            SubjectIds = subjectsByClass.TryGetValue(row.ClassId, out List<Guid>? subjects) ? subjects : []
        }).ToList();

        return new TeacherAssignmentsResponseDto
        {
            TeacherId = teacherId,
            AcademicYearId = academicYearId,
            ClassAssignments = assignments
        };
    }

    public async Task SaveAssignmentsAsync(
        Guid teacherId,
        SaveTeacherAssignmentsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string schema = _context.OperationalSchema;
        Guid? academicYearId = request.AcademicYearId
            ?? await _scopeMapping.GetActiveAcademicYearIdAsync(schema, cancellationToken).ConfigureAwait(false);

        if (!academicYearId.HasValue)
        {
            throw new InvalidOperationException("No active academic year found.");
        }

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {schema}.{DatabaseConfig.TableTeacherClassAssignments}
SET isactive = false, updatedon = NOW()
WHERE teacherid = @TeacherId AND academicyearid = @AcademicYearId
""",
                new { TeacherId = teacherId, AcademicYearId = academicYearId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {schema}.{DatabaseConfig.TableTeacherSubjectAssignments}
SET isactive = false, updatedon = NOW()
WHERE teacherid = @TeacherId AND academicyearid = @AcademicYearId
""",
                new { TeacherId = teacherId, AcademicYearId = academicYearId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        Guid? primaryClassId = null;

        foreach (TeacherClassAssignmentRowDto row in request.ClassAssignments.Where(r => r.ClassId != Guid.Empty))
        {
            await UpsertClassAssignmentAsync(
                connection,
                schema,
                teacherId,
                row,
                academicYearId.Value,
                cancellationToken).ConfigureAwait(false);

            if (row.IsClassTeacher && primaryClassId is null)
            {
                primaryClassId = row.ClassId;
            }

            foreach (Guid subjectId in row.SubjectIds.Distinct().Where(id => id != Guid.Empty))
            {
                await UpsertSubjectAssignmentAsync(
                    connection,
                    schema,
                    teacherId,
                    row.ClassId,
                    subjectId,
                    academicYearId.Value,
                    row.CanAddMarks,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                $"""
UPDATE {schema}.{DatabaseConfig.TableTeachers}
SET classid = @ClassId, updatedon = NOW(), versionno = versionno + 1
WHERE id = @TeacherId
""",
                new { TeacherId = teacherId, ClassId = primaryClassId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        string? userIdSql = $"""
SELECT userid FROM {schema}.{DatabaseConfig.TableTeachers}
WHERE id = @TeacherId AND userid IS NOT NULL
""";
        Guid? userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(userIdSql, new { TeacherId = teacherId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (userId.HasValue
            && !string.IsNullOrWhiteSpace(_tenantProvider.GetCurrentSchoolId())
            && Guid.TryParse(_tenantProvider.GetCurrentSchoolId(), out Guid schoolId))
        {
            await _userScopeService.BumpScopeVersionAsync(userId.Value, schoolId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertClassAssignmentAsync(
        IDbConnection connection,
        string schema,
        Guid teacherId,
        TeacherClassAssignmentRowDto row,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableTeacherClassAssignments}
    (id, teacherid, classid, academicyearid, isclassteacher,
     canviewstudents, canmarkattendance, canaddmarks, cansendnotice,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES (gen_random_uuid(), @TeacherId, @ClassId, @AcademicYearId, @IsClassTeacher,
        @CanViewStudents, @CanMarkAttendance, @CanAddMarks, @CanSendNotice,
        true, 1, '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW())
ON CONFLICT ON CONSTRAINT uq_teacherclassassignments DO UPDATE SET
    isclassteacher = EXCLUDED.isclassteacher,
    canviewstudents = EXCLUDED.canviewstudents,
    canmarkattendance = EXCLUDED.canmarkattendance,
    canaddmarks = EXCLUDED.canaddmarks,
    cansendnotice = EXCLUDED.cansendnotice,
    isactive = true,
    updatedon = NOW(),
    versionno = {schema}.{DatabaseConfig.TableTeacherClassAssignments}.versionno + 1
""";

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    TeacherId = teacherId,
                    row.ClassId,
                    AcademicYearId = academicYearId,
                    row.IsClassTeacher,
                    row.CanViewStudents,
                    row.CanMarkAttendance,
                    row.CanAddMarks,
                    row.CanSendNotice
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpsertSubjectAssignmentAsync(
        IDbConnection connection,
        string schema,
        Guid teacherId,
        Guid classId,
        Guid subjectId,
        Guid academicYearId,
        bool canAddMarks,
        CancellationToken cancellationToken)
    {
        string sql = $"""
INSERT INTO {schema}.{DatabaseConfig.TableTeacherSubjectAssignments}
    (id, teacherid, subjectid, classid, academicyearid, canaddmarks,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES (gen_random_uuid(), @TeacherId, @SubjectId, @ClassId, @AcademicYearId, @CanAddMarks,
        true, 1, '{DatabaseConfig.SystemUserId}', NOW(), '{DatabaseConfig.SystemUserId}', NOW())
ON CONFLICT ON CONSTRAINT uq_teachersubjectassignments DO UPDATE SET
    canaddmarks = EXCLUDED.canaddmarks,
    isactive = true,
    updatedon = NOW(),
    versionno = {schema}.{DatabaseConfig.TableTeacherSubjectAssignments}.versionno + 1
""";

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { TeacherId = teacherId, SubjectId = subjectId, ClassId = classId, AcademicYearId = academicYearId, CanAddMarks = canAddMarks },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class ClassAssignmentRow
    {
        public Guid ClassId { get; init; }

        public bool IsClassTeacher { get; init; }

        public bool CanViewStudents { get; init; }

        public bool CanMarkAttendance { get; init; }

        public bool CanAddMarks { get; init; }

        public bool CanSendNotice { get; init; }
    }

    private sealed class SubjectAssignmentRow
    {
        public Guid ClassId { get; init; }

        public Guid SubjectId { get; init; }
    }
}
