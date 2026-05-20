using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Attendance.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;
using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Infrastructure.Modules.Attendance;

public sealed class AttendanceRepository : BaseRepository, IAttendanceRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public AttendanceRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
    }

    private string AttendanceSchema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    public async Task<IList<AttendanceEntity>> GetByClassAndDateAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND attendancedate = @Date
              AND isactive = true
            ORDER BY studentid;
            """;

        IEnumerable<AttendanceEntity> result = await connection.QueryAsync<AttendanceEntity>(
            new CommandDefinition(sql, new { ClassId = classId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);

        return result.ToList();
    }

    public async Task<AttendanceEntity?> GetByStudentAndDateAsync(
        Guid studentId,
        DateOnly date,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
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
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, classid, studentid, teacherid,
                   attendancedate, status, remarks,
                   isactive, versionno,
                   createdby, createdon, updatedby, updatedon
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            WHERE studentid = @StudentId
              AND attendancedate >= @From
              AND attendancedate <= @To
              AND isactive = true
            ORDER BY attendancedate;
            """;

        IEnumerable<AttendanceEntity> result = await connection.QueryAsync<AttendanceEntity>(
            new CommandDefinition(sql, new { StudentId = studentId, From = from, To = to }, cancellationToken: ct))
            .ConfigureAwait(false);

        return result.ToList();
    }

    public async Task UpsertAsync(AttendanceEntity attendance, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SaveAttendanceAsync(conn, tx, attendance, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task BulkUpsertAsync(IList<AttendanceEntity> records, CancellationToken ct = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            Guid classId = records[0].ClassId;
            DateOnly attendanceDate = records[0].AttendanceDate;

            Dictionary<Guid, ExistingAttendanceRow> existingByStudent = await GetExistingByClassAndDateAsync(
                conn,
                tx,
                classId,
                attendanceDate,
                ct).ConfigureAwait(false);

            foreach (AttendanceEntity record in records)
            {
                if (existingByStudent.TryGetValue(record.StudentId, out ExistingAttendanceRow? existing))
                {
                    await UpdateAttendanceAsync(conn, tx, existing.Id, record, ct).ConfigureAwait(false);
                }
                else
                {
                    await InsertAttendanceAsync(conn, tx, record, ct).ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> IsSubmittedAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT COUNT(1)
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND attendancedate = @Date
              AND isactive = true;
            """;

        int count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ClassId = classId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);

        return count > 0;
    }

    /// <summary>Insert when no row exists; otherwise update the existing row.</summary>
    private async Task SaveAttendanceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        AttendanceEntity attendance,
        CancellationToken ct)
    {
        ExistingAttendanceRow? existing = await FindExistingAsync(
            connection,
            transaction,
            attendance.ClassId,
            attendance.StudentId,
            attendance.AttendanceDate,
            ct).ConfigureAwait(false);

        if (existing is null)
        {
            await InsertAttendanceAsync(connection, transaction, attendance, ct).ConfigureAwait(false);
            return;
        }

        await UpdateAttendanceAsync(connection, transaction, existing.Id, attendance, ct).ConfigureAwait(false);
    }

    private async Task<ExistingAttendanceRow?> FindExistingAsync(
        IDbConnection connection,
        IDbTransaction? transaction,
        Guid classId,
        Guid studentId,
        DateOnly attendanceDate,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT id AS Id, studentid AS StudentId
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND studentid = @StudentId
              AND attendancedate = @AttendanceDate
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExistingAttendanceRow>(
            new CommandDefinition(
                sql,
                new { ClassId = classId, StudentId = studentId, AttendanceDate = attendanceDate },
                transaction,
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, ExistingAttendanceRow>> GetExistingByClassAndDateAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid classId,
        DateOnly attendanceDate,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT id AS Id, studentid AS StudentId
            FROM {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            WHERE classid = @ClassId
              AND attendancedate = @AttendanceDate;
            """;

        IEnumerable<ExistingAttendanceRow> rows = await connection.QueryAsync<ExistingAttendanceRow>(
            new CommandDefinition(
                sql,
                new { ClassId = classId, AttendanceDate = attendanceDate },
                transaction,
                cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.StudentId);
    }

    private async Task InsertAttendanceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        AttendanceEntity attendance,
        CancellationToken ct)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();

        attendance.Id = attendance.Id == Guid.Empty ? Guid.NewGuid() : attendance.Id;
        EnsureInsertAudit(attendance, utcNow, actorId);

        string sql = $"""
            INSERT INTO {AttendanceSchema}.{DatabaseConfig.TableAttendance}
                (id, classid, studentid, teacherid,
                 attendancedate, status, remarks,
                 isactive, versionno,
                 createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ClassId, @StudentId, @TeacherId,
                 @AttendanceDate, @Status, @Remarks,
                 true, 1,
                 @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(sql, attendance, transaction, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task UpdateAttendanceAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid existingId,
        AttendanceEntity attendance,
        CancellationToken ct)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();

        string sql = $"""
            UPDATE {AttendanceSchema}.{DatabaseConfig.TableAttendance}
            SET status = @Status,
                remarks = @Remarks,
                teacherid = @TeacherId,
                isactive = true,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Id = existingId,
                    attendance.Status,
                    attendance.Remarks,
                    attendance.TeacherId,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow
                },
                transaction,
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private sealed class ExistingAttendanceRow
    {
        public Guid Id { get; init; }

        public Guid StudentId { get; init; }
    }
}
