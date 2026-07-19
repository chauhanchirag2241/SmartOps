using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Exam;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Exam;

public sealed class ExamMarksRepository : BaseRepository, IExamMarksRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public ExamMarksRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    public async Task<IList<ExamStudentRosterRow>> GetClassStudentsAsync(Guid classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT st.id AS StudentId,
                   TRIM(COALESCE(st.firstname, '') || ' ' || COALESCE(st.lastname, '')) AS StudentName,
                   COALESCE(sa.rollnumber, '') AS RollNo
            FROM {Schema}.{DatabaseConfig.TableStudents} st
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = @ClassId AND cl.isactive = true
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                ON sa.studentid = st.id
               AND sa.classid = @ClassId
               AND sa.academicyearid = cl.academicyearid
               AND sa.isactive = true
            WHERE st.isactive = true
            ORDER BY sa.rollnumber NULLS LAST, st.firstname, st.lastname;
            """;

        IEnumerable<ExamStudentRosterRow> rows = await connection.QueryAsync<ExamStudentRosterRow>(
                new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ExamStudentMarkEntity>> GetMarksByScheduleAsync(Guid scheduleId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examscheduleid, componentid, studentid, marksobtained, isabsent, isexempted, remark,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamStudentMarks}
            WHERE examscheduleid = @ScheduleId AND isactive = true;
            """;

        IEnumerable<ExamStudentMarkEntity> rows = await connection.QueryAsync<ExamStudentMarkEntity>(
                new CommandDefinition(sql, new { ScheduleId = scheduleId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ExamMarkWithSubjectRow>> GetMarksByExamClassAsync(
        Guid examId,
        Guid classId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT m.examscheduleid AS ExamScheduleId,
                   sc.subjectid AS SubjectId,
                   m.componentid AS ComponentId,
                   m.studentid AS StudentId,
                   m.marksobtained AS MarksObtained,
                   m.isabsent AS IsAbsent
            FROM {Schema}.{DatabaseConfig.TableExamStudentMarks} m
            INNER JOIN {Schema}.{DatabaseConfig.TableExamSchedules} sc
                ON sc.id = m.examscheduleid AND sc.isactive = true
            WHERE sc.examid = @ExamId AND sc.classid = @ClassId AND m.isactive = true;
            """;

        IEnumerable<ExamMarkWithSubjectRow> rows = await connection.QueryAsync<ExamMarkWithSubjectRow>(
                new CommandDefinition(sql, new { ExamId = examId, ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task BulkUpsertMarksAsync(
        Guid scheduleId,
        IList<ExamStudentMarkEntity> marks,
        CancellationToken ct = default)
    {
        if (marks.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (ExamStudentMarkEntity mark in marks)
            {
                mark.Id = mark.Id == Guid.Empty ? Guid.NewGuid() : mark.Id;
                mark.ExamScheduleId = scheduleId;
                EnsureInsertAudit(mark, utcNow, actorId);

                string sql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableExamStudentMarks}
                        (id, examscheduleid, componentid, studentid, marksobtained, isabsent, isexempted, remark,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @ExamScheduleId, @ComponentId, @StudentId, @MarksObtained, @IsAbsent, @IsExempted, @Remark,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
                    ON CONFLICT ON CONSTRAINT uq_examstudentmarks_schedule_component_student
                    DO UPDATE SET marksobtained = @MarksObtained,
                                  isabsent = @IsAbsent,
                                  isexempted = @IsExempted,
                                  remark = @Remark,
                                  isactive = true,
                                  updatedby = @UpdatedBy,
                                  updatedon = @UpdatedOn,
                                  versionno = {DatabaseConfig.TableExamStudentMarks}.versionno + 1;
                    """;

                await conn.ExecuteAsync(new CommandDefinition(sql, mark, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task<IList<ExamSubjectProgressRow>> GetSubjectProgressAsync(
        Guid examId,
        Guid classId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT sc.id AS ExamScheduleId,
                   sc.subjectid AS SubjectId,
                   COALESCE(s.subjectname, '') AS SubjectName,
                   COALESCE((SELECT COUNT(DISTINCT m.studentid)
                             FROM {Schema}.{DatabaseConfig.TableExamStudentMarks} m
                             WHERE m.examscheduleid = sc.id AND m.isactive = true
                               AND (m.marksobtained IS NOT NULL OR m.isabsent = true)), 0)::int AS Entered
            FROM {Schema}.{DatabaseConfig.TableExamSchedules} sc
            LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} s ON s.id = sc.subjectid
            WHERE sc.examid = @ExamId AND sc.classid = @ClassId AND sc.isactive = true
            ORDER BY sc.examdate, s.subjectname;
            """;

        IEnumerable<ExamSubjectProgressRow> rows = await connection.QueryAsync<ExamSubjectProgressRow>(
                new CommandDefinition(sql, new { ExamId = examId, ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }
}
