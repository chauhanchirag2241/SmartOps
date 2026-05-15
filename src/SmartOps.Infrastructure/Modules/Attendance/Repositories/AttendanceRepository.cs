using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Configuration;
using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Infrastructure.Modules.Attendance.Repositories;

public sealed class AttendanceRepository : BaseRepository, IAttendanceRepository
{
    public AttendanceRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IList<AttendanceEntity>> GetByClassAndDateAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        const string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {DatabaseConfig.Schema_School}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND attendancedate = @Date
              AND isactive = true
            ORDER BY studentid;
            """;

        var result = await connection.QueryAsync<AttendanceEntity>(
            new CommandDefinition(sql, new { ClassId = classId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);

        return result.ToList();
    }

    public async Task<AttendanceEntity?> GetByStudentAndDateAsync(
        Guid studentId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        const string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {DatabaseConfig.Schema_School}.{DatabaseConfig.TableAttendance}
            WHERE studentid = @StudentId
              AND attendancedate = @Date
              AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<AttendanceEntity>(
            new CommandDefinition(sql, new { StudentId = studentId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<AttendanceEntity>> GetByStudentAndRangeAsync(
        Guid studentId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        const string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {DatabaseConfig.Schema_School}.{DatabaseConfig.TableAttendance}
            WHERE studentid = @StudentId
              AND attendancedate >= @From
              AND attendancedate <= @To
              AND isactive = true
            ORDER BY attendancedate;
            """;

        var result = await connection.QueryAsync<AttendanceEntity>(
            new CommandDefinition(sql, new { StudentId = studentId, From = from, To = to }, cancellationToken: ct))
            .ConfigureAwait(false);

        return result.ToList();
    }

    public async Task UpsertAsync(AttendanceEntity attendance, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpsertInternalAsync(conn, tx, attendance, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task BulkUpsertAsync(IList<AttendanceEntity> records, CancellationToken ct = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (var record in records)
            {
                await UpsertInternalAsync(conn, tx, record, ct).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> IsSubmittedAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        const string sql = $"""
            SELECT COUNT(1)
            FROM {DatabaseConfig.Schema_School}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND attendancedate = @Date
              AND isactive = true;
            """;

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ClassId = classId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);

        return count > 0;
    }

    private async Task UpsertInternalAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        AttendanceEntity attendance,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (attendance.Id == Guid.Empty)
        {
            attendance.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(attendance, now, Guid.Parse(DatabaseConfig.SystemUserId));

        const string sql = $"""
            INSERT INTO {DatabaseConfig.Schema_School}.{DatabaseConfig.TableAttendance} AS a
                (id, classid, studentid, teacherid,
                 attendancedate, status, remarks,
                 isactive, versionno,
                 createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ClassId, @StudentId, @TeacherId,
                 @AttendanceDate, @Status, @Remarks,
                 true, 1,
                 @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
            ON CONFLICT (classid, studentid, attendancedate)
            DO UPDATE SET
                status = EXCLUDED.status,
                remarks = EXCLUDED.remarks,
                teacherid = EXCLUDED.teacherid,
                isactive = true,
                updatedby = EXCLUDED.updatedby,
                updatedon = EXCLUDED.updatedon,
                versionno = a.versionno + 1
            WHERE a.status IS DISTINCT FROM EXCLUDED.status
               OR a.remarks IS DISTINCT FROM EXCLUDED.remarks
               OR a.teacherid IS DISTINCT FROM EXCLUDED.teacherid
               OR a.isactive = false;
            """;



        await connection.ExecuteAsync(
            new CommandDefinition(sql, attendance, transaction, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
