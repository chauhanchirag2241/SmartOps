using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Exam;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Exam;

public sealed class ExamResultRepository : BaseRepository, IExamResultRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public ExamResultRepository(
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

    public async Task UpsertResultsAsync(
        Guid examId,
        Guid classId,
        IList<ExamResultEntity> results,
        CancellationToken ct = default)
    {
        if (results.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (ExamResultEntity result in results)
            {
                result.Id = result.Id == Guid.Empty ? Guid.NewGuid() : result.Id;
                result.ExamId = examId;
                result.ClassId = classId;
                EnsureInsertAudit(result, utcNow, actorId);

                string sql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableExamResults}
                        (id, examid, classid, studentid, totalmarks, maxmarks, percentage, grade, rank, result,
                         subjectresults, declaredon, declaredby,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @ExamId, @ClassId, @StudentId, @TotalMarks, @MaxMarks, @Percentage, @Grade, @Rank, @Result,
                         @SubjectResults, @DeclaredOn, @DeclaredBy,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
                    ON CONFLICT ON CONSTRAINT uq_examresults_exam_student
                    DO UPDATE SET classid = @ClassId,
                                  totalmarks = @TotalMarks,
                                  maxmarks = @MaxMarks,
                                  percentage = @Percentage,
                                  grade = @Grade,
                                  rank = @Rank,
                                  result = @Result,
                                  subjectresults = @SubjectResults,
                                  isactive = true,
                                  updatedby = @UpdatedBy,
                                  updatedon = @UpdatedOn,
                                  versionno = {DatabaseConfig.TableExamResults}.versionno + 1;
                    """;

                await conn.ExecuteAsync(new CommandDefinition(sql, result, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task<IList<ExamResultEntity>> GetResultsAsync(Guid examId, Guid? classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examid, classid, studentid, totalmarks, maxmarks, percentage, grade, rank, result,
                   subjectresults, declaredon, declaredby,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamResults}
            WHERE examid = @ExamId AND isactive = true
              AND (@ClassId IS NULL OR classid = @ClassId)
            ORDER BY rank;
            """;

        IEnumerable<ExamResultEntity> rows = await connection.QueryAsync<ExamResultEntity>(
                new CommandDefinition(sql, new { ExamId = examId, ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ExamResultEntity?> GetStudentResultAsync(Guid examId, Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examid, classid, studentid, totalmarks, maxmarks, percentage, grade, rank, result,
                   subjectresults, declaredon, declaredby,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamResults}
            WHERE examid = @ExamId AND studentid = @StudentId AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExamResultEntity>(
                new CommandDefinition(sql, new { ExamId = examId, StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task MarkResultsDeclaredAsync(
        Guid examId,
        Guid classId,
        DateTime declaredOn,
        Guid declaredBy,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamResults}
            SET declaredon = @DeclaredOn,
                declaredby = @DeclaredBy,
                updatedby = @DeclaredBy,
                updatedon = @DeclaredOn,
                versionno = versionno + 1
            WHERE examid = @ExamId AND classid = @ClassId AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ExamId = examId, ClassId = classId, DeclaredOn = declaredOn, DeclaredBy = declaredBy },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ExamHallTicketEntity>> GetHallTicketsAsync(Guid examId, Guid classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examid, classid, studentid, ticketno, seatno,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamHallTickets}
            WHERE examid = @ExamId AND classid = @ClassId AND isactive = true
            ORDER BY ticketno;
            """;

        IEnumerable<ExamHallTicketEntity> rows = await connection.QueryAsync<ExamHallTicketEntity>(
                new CommandDefinition(sql, new { ExamId = examId, ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task BulkInsertHallTicketsAsync(IList<ExamHallTicketEntity> tickets, CancellationToken ct = default)
    {
        if (tickets.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (ExamHallTicketEntity ticket in tickets)
            {
                ticket.Id = ticket.Id == Guid.Empty ? Guid.NewGuid() : ticket.Id;
                EnsureInsertAudit(ticket, utcNow, actorId);

                string sql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableExamHallTickets}
                        (id, examid, classid, studentid, ticketno, seatno,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @ExamId, @ClassId, @StudentId, @TicketNo, @SeatNo,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
                    ON CONFLICT ON CONSTRAINT uq_examhalltickets_exam_student
                    DO UPDATE SET classid = @ClassId,
                                  ticketno = @TicketNo,
                                  seatno = @SeatNo,
                                  isactive = true,
                                  updatedby = @UpdatedBy,
                                  updatedon = @UpdatedOn,
                                  versionno = {DatabaseConfig.TableExamHallTickets}.versionno + 1;
                    """;

                await conn.ExecuteAsync(new CommandDefinition(sql, ticket, tx, cancellationToken: ct)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }
}
