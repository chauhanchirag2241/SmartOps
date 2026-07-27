using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.StaffAttendance;
using SmartOps.Domain.Modules.StaffAttendance.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.StaffAttendance;

public sealed class StaffAttendanceRepository : BaseRepository, IStaffAttendanceRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public StaffAttendanceRepository(
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

    public async Task<IList<StaffAttendanceListRow>> ListByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT
                COALESCE(sa.id, '00000000-0000-0000-0000-000000000000'::uuid) AS Id,
                e.id AS EmployeeId,
                TRIM(e.firstname || ' ' || e.lastname) AS EmployeeName,
                e.departmentid AS DepartmentId,
                d.name AS DepartmentName,
                @Date AS AttendanceDate,
                sa.checkintime AS CheckInTime,
                sa.checkouttime AS CheckOutTime,
                sa.checkinsource AS CheckInSource,
                sa.checkoutsource AS CheckOutSource,
                COALESCE(sa.status, 0)::smallint AS Status,
                sa.remarks AS Remarks,
                sa.checkinconfidence AS CheckInConfidence,
                sa.checkoutconfidence AS CheckOutConfidence,
                EXISTS (
                    SELECT 1 FROM {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments} fe
                    WHERE fe.employeeid = e.id AND fe.isactive = true
                ) AS IsFaceEnrolled,
                e.photourl AS PhotoUrl,
                e.shiftstarttime AS ShiftStartTime
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            LEFT JOIN {Schema}.{DatabaseConfig.TableDepartments} d ON d.id = e.departmentid AND d.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableStaffAttendance} sa
                ON sa.employeeid = e.id AND sa.attendancedate = @Date AND sa.isactive = true
            WHERE e.isactive = true
            ORDER BY e.firstname ASC, e.lastname ASC;
            """;

        IEnumerable<StaffAttendanceListRow> rows = await connection.QueryAsync<StaffAttendanceListRow>(
            new CommandDefinition(sql, new { Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<StaffAttendanceEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, employeeid, attendancedate, checkintime, checkouttime,
                   checkinsource, checkoutsource, status, remarks,
                   checkinconfidence, checkoutconfidence, markedbyuserid,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableStaffAttendance}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<StaffAttendanceEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<StaffAttendanceEntity?> GetByEmployeeAndDateAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, employeeid, attendancedate, checkintime, checkouttime,
                   checkinsource, checkoutsource, status, remarks,
                   checkinconfidence, checkoutconfidence, markedbyuserid,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableStaffAttendance}
            WHERE employeeid = @EmployeeId AND attendancedate = @Date AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<StaffAttendanceEntity>(
            new CommandDefinition(sql, new { EmployeeId = employeeId, Date = date }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> UpsertPunchAsync(StaffAttendanceEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            EnsureInsertAudit(entity, utcNow, actorId);

            string insertSql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableStaffAttendance}
                    (id, employeeid, attendancedate, checkintime, checkouttime,
                     checkinsource, checkoutsource, status, remarks,
                     checkinconfidence, checkoutconfidence, markedbyuserid,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @EmployeeId, @AttendanceDate, @CheckInTime, @CheckOutTime,
                     @CheckInSource, @CheckOutSource, @Status, @Remarks,
                     @CheckInConfidence, @CheckOutConfidence, @MarkedByUserId,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;

            await connection.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                entity.Id,
                entity.EmployeeId,
                entity.AttendanceDate,
                entity.CheckInTime,
                entity.CheckOutTime,
                entity.CheckInSource,
                entity.CheckOutSource,
                Status = (short)entity.Status,
                entity.Remarks,
                entity.CheckInConfidence,
                entity.CheckOutConfidence,
                entity.MarkedByUserId,
                entity.IsActive,
                entity.VersionNo,
                entity.CreatedBy,
                entity.CreatedOn,
                entity.UpdatedBy,
                entity.UpdatedOn
            }, cancellationToken: ct)).ConfigureAwait(false);

            return entity.Id;
        }

        ApplyUpdateAudit(entity, ResolveUpdateActor(), utcNow);

        string updateSql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableStaffAttendance}
            SET checkintime = @CheckInTime,
                checkouttime = @CheckOutTime,
                checkinsource = @CheckInSource,
                checkoutsource = @CheckOutSource,
                status = @Status,
                remarks = @Remarks,
                checkinconfidence = @CheckInConfidence,
                checkoutconfidence = @CheckOutConfidence,
                markedbyuserid = @MarkedByUserId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(updateSql, new
        {
            entity.Id,
            entity.CheckInTime,
            entity.CheckOutTime,
            entity.CheckInSource,
            entity.CheckOutSource,
            Status = (short)entity.Status,
            entity.Remarks,
            entity.CheckInConfidence,
            entity.CheckOutConfidence,
            entity.MarkedByUserId,
            entity.UpdatedBy,
            entity.UpdatedOn
        }, cancellationToken: ct)).ConfigureAwait(false);

        return entity.Id;
    }

    public async Task UpdateAsync(StaffAttendanceEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableStaffAttendance}
            SET checkintime = @CheckInTime,
                checkouttime = @CheckOutTime,
                checkinsource = @CheckInSource,
                checkoutsource = @CheckOutSource,
                status = @Status,
                remarks = @Remarks,
                checkinconfidence = @CheckInConfidence,
                checkoutconfidence = @CheckOutConfidence,
                markedbyuserid = @MarkedByUserId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            entity.CheckInTime,
            entity.CheckOutTime,
            entity.CheckInSource,
            entity.CheckOutSource,
            Status = (short)entity.Status,
            entity.Remarks,
            entity.CheckInConfidence,
            entity.CheckOutConfidence,
            entity.MarkedByUserId,
            entity.UpdatedBy,
            entity.UpdatedOn
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id FROM {Schema}.{DatabaseConfig.TableEmployees}
            WHERE userid = @UserId AND isactive = true LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<EmployeeShiftInfo?> GetEmployeeInfoAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT
                e.id AS Id,
                TRIM(e.firstname || ' ' || e.lastname) AS EmployeeName,
                e.departmentid AS DepartmentId,
                d.name AS DepartmentName,
                e.shiftstarttime AS ShiftStartTime,
                e.photourl AS PhotoUrl,
                EXISTS (
                    SELECT 1 FROM {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments} fe
                    WHERE fe.employeeid = e.id AND fe.isactive = true
                ) AS IsFaceEnrolled
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            LEFT JOIN {Schema}.{DatabaseConfig.TableDepartments} d ON d.id = e.departmentid AND d.isactive = true
            WHERE e.id = @EmployeeId AND e.isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<EmployeeShiftInfo>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task UpdateEmployeePhotoUrlAsync(Guid employeeId, string photoUrl, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployees}
            SET photourl = @PhotoUrl,
                updatedon = @Now,
                updatedby = @Actor,
                versionno = versionno + 1
            WHERE id = @EmployeeId AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            EmployeeId = employeeId,
            PhotoUrl = photoUrl,
            Now = DateTime.UtcNow,
            Actor = ResolveUpdateActor()
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<StaffAttendanceReportSourceRow>> GetReportSourceAsync(
        int month,
        int year,
        Guid? departmentId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateOnly from = new(year, month, 1);
        DateOnly to = from.AddMonths(1).AddDays(-1);

        string sql = $"""
            SELECT
                e.id AS EmployeeId,
                TRIM(e.firstname || ' ' || e.lastname) AS EmployeeName,
                d.name AS DepartmentName,
                sa.attendancedate AS AttendanceDate,
                sa.status AS Status
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            LEFT JOIN {Schema}.{DatabaseConfig.TableDepartments} d ON d.id = e.departmentid AND d.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableStaffAttendance} sa
                ON sa.employeeid = e.id
               AND sa.attendancedate >= @From
               AND sa.attendancedate <= @To
               AND sa.isactive = true
            WHERE e.isactive = true
              AND (@DepartmentId IS NULL OR e.departmentid = @DepartmentId)
            ORDER BY e.firstname ASC, e.lastname ASC, sa.attendancedate ASC;
            """;

        IEnumerable<StaffAttendanceReportSourceRow> rows = await connection.QueryAsync<StaffAttendanceReportSourceRow>(
            new CommandDefinition(sql, new { From = from, To = to, DepartmentId = departmentId }, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToList();
    }
}
