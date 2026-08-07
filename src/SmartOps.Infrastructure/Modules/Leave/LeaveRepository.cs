using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Leave;

public sealed class LeaveRepository : BaseRepository, ILeaveRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public LeaveRepository(
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

    private string G => IdentitySchema;

    public async Task<Guid> CreateAsync(LeaveRequestEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, now, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableLeaveRequests}
                (id, requesttype, employeeid, studentid, requestedbyuserid, fromdate, todate,
                 leavetype, leavetypeid, totaldays, ishalfday, deductedfrombalance,
                 reason, status, approvedbyuserid, approvedon, approverremark,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @RequestType, @EmployeeId, @StudentId, @RequestedByUserId, @FromDate, @ToDate,
                 @LeaveType, @LeaveTypeId, @TotalDays, @IsHalfDay, @DeductedFromBalance,
                 @Reason, @Status, @ApprovedByUserId, @ApprovedOn, @ApproverRemark,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            RequestType = (short)entity.RequestType,
            entity.EmployeeId,
            entity.StudentId,
            entity.RequestedByUserId,
            entity.FromDate,
            entity.ToDate,
            LeaveType = entity.LeaveType.HasValue ? (short?)entity.LeaveType : null,
            entity.LeaveTypeId,
            entity.TotalDays,
            entity.IsHalfDay,
            entity.DeductedFromBalance,
            entity.Reason,
            Status = (short)entity.Status,
            entity.ApprovedByUserId,
            entity.ApprovedOn,
            entity.ApproverRemark,
            entity.IsActive,
            entity.VersionNo,
            entity.CreatedBy,
            entity.CreatedOn,
            entity.UpdatedBy,
            entity.UpdatedOn
        }, cancellationToken: ct)).ConfigureAwait(false);

        return entity.Id;
    }

    public async Task UpdateAsync(LeaveRequestEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableLeaveRequests}
            SET status = @Status,
                approvedbyuserid = @ApprovedByUserId,
                approvedon = @ApprovedOn,
                approverremark = @ApproverRemark,
                reason = @Reason,
                fromdate = @FromDate,
                todate = @ToDate,
                leavetype = @LeaveType,
                leavetypeid = @LeaveTypeId,
                totaldays = @TotalDays,
                ishalfday = @IsHalfDay,
                deductedfrombalance = @DeductedFromBalance,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            entity.Id,
            Status = (short)entity.Status,
            entity.ApprovedByUserId,
            entity.ApprovedOn,
            entity.ApproverRemark,
            entity.Reason,
            entity.FromDate,
            entity.ToDate,
            LeaveType = entity.LeaveType.HasValue ? (short?)entity.LeaveType : null,
            entity.LeaveTypeId,
            entity.TotalDays,
            entity.IsHalfDay,
            entity.DeductedFromBalance,
            entity.UpdatedBy,
            entity.UpdatedOn
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<LeaveRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, requesttype AS RequestType, employeeid AS EmployeeId, studentid AS StudentId,
                   requestedbyuserid AS RequestedByUserId, fromdate AS FromDate, todate AS ToDate,
                   leavetype AS LeaveType, leavetypeid AS LeaveTypeId, totaldays AS TotalDays,
                   ishalfday AS IsHalfDay, deductedfrombalance AS DeductedFromBalance,
                   reason AS Reason, status AS Status,
                   approvedbyuserid AS ApprovedByUserId, approvedon AS ApprovedOn, approverremark AS ApproverRemark,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveRequests}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<LeaveRequestEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public Task<IList<LeaveListRow>> GetStaffListAsync(string? statusFilter, Guid? employeeid, DateOnly? from, DateOnly? to, CancellationToken ct = default) =>
        GetListInternalAsync(LeaveRequestType.Staff, statusFilter, employeeid, null, from, to, null, ct);

    public Task<IList<LeaveListRow>> GetStudentListAsync(string? statusFilter, Guid? studentId, CancellationToken ct = default) =>
        GetListInternalAsync(LeaveRequestType.Student, statusFilter, null, studentId, null, null, null, ct);

    public Task<IList<LeaveListRow>> GetMineAsync(LeaveRequestType requestType, Guid userId, CancellationToken ct = default) =>
        GetListInternalAsync(requestType, null, null, null, null, null, userId, ct);

    private async Task<IList<LeaveListRow>> GetListInternalAsync(
        LeaveRequestType requestType,
        string? statusFilter,
        Guid? employeeid,
        Guid? studentId,
        DateOnly? from,
        DateOnly? to,
        Guid? requestedByUserId,
        CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder($"""
            SELECT lr.id AS Id, lr.requesttype AS RequestType, lr.employeeid AS EmployeeId,
                   tu.firstname AS TeacherFirstName, tu.lastname AS TeacherLastName,
                   lr.studentid AS StudentId, su.firstname AS StudentFirstName, su.lastname AS StudentLastName,
                   {DashboardClassLabel.DisplayNameSql} AS ClassName,
                   lr.requestedbyuserid AS RequestedByUserId, u.email AS RequestedByEmail,
                   lr.fromdate AS FromDate, lr.todate AS ToDate, lr.leavetype AS LeaveType,
                   lr.leavetypeid AS LeaveTypeId, lt.name AS LeaveTypeName,
                   lr.totaldays AS TotalDays, lr.ishalfday AS IsHalfDay,
                   lr.status AS Status, lr.createdon AS CreatedOn,
                   lr.reason AS Reason,
                   lr.approvedbyuserid AS ApprovedByUserId, au.email AS ApprovedByEmail,
                   au.firstname AS ApprovedByFirstName, au.lastname AS ApprovedByLastName,
                   lr.approvedon AS ApprovedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveRequests} lr
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = lr.employeeid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} tu ON tu.id = t.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = lr.studentid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} su ON su.id = s.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = lr.requestedbyuserid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} au ON au.id = lr.approvedbyuserid
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = lr.leavetypeid
            WHERE lr.isactive = true AND lr.requesttype = @RequestType
            """);

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<LeaveRequestStatus>(statusFilter, true, out LeaveRequestStatus st))
        {
            sb.Append(" AND lr.status = @Status");
        }

        if (employeeid.HasValue)
        {
            sb.Append(" AND lr.employeeid = @EmployeeId");
        }

        if (studentId.HasValue)
        {
            sb.Append(" AND lr.studentid = @StudentId");
        }

        if (from.HasValue)
        {
            sb.Append(" AND lr.todate >= @From");
        }

        if (to.HasValue)
        {
            sb.Append(" AND lr.fromdate <= @To");
        }

        if (requestedByUserId.HasValue)
        {
            sb.Append(" AND lr.requestedbyuserid = @RequestedByUserId");
        }

        sb.Append(" ORDER BY lr.createdon DESC");

        short? statusVal = null;
        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<LeaveRequestStatus>(statusFilter, true, out LeaveRequestStatus parsed))
        {
            statusVal = (short)parsed;
        }

        var rows = await connection.QueryAsync<LeaveListRow>(new CommandDefinition(
            sb.ToString(),
            new
            {
                RequestType = (short)requestType,
                Status = statusVal,
                EmployeeId = employeeid,
                StudentId = studentId,
                From = from,
                To = to,
                RequestedByUserId = requestedByUserId
            },
            cancellationToken: ct)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<LeaveDetailRow?> GetDetailRowAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT lr.id AS Id, lr.requesttype AS RequestType, lr.employeeid AS EmployeeId,
                   tu.firstname AS TeacherFirstName, tu.lastname AS TeacherLastName,
                   lr.studentid AS StudentId, su.firstname AS StudentFirstName, su.lastname AS StudentLastName,
                   {DashboardClassLabel.DisplayNameSql} AS ClassName,
                   lr.requestedbyuserid AS RequestedByUserId, ru.email AS RequestedByEmail,
                   lr.fromdate AS FromDate, lr.todate AS ToDate, lr.leavetype AS LeaveType,
                   lr.leavetypeid AS LeaveTypeId, lt.name AS LeaveTypeName,
                   lr.totaldays AS TotalDays, lr.ishalfday AS IsHalfDay,
                   lr.status AS Status, lr.reason AS Reason,
                   lr.approvedbyuserid AS ApprovedByUserId, au.email AS ApprovedByEmail,
                   lr.approvedon AS ApprovedOn, lr.approverremark AS ApproverRemark,
                   lr.createdon AS CreatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveRequests} lr
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = lr.employeeid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} tu ON tu.id = t.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = lr.studentid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} su ON su.id = s.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} ru ON ru.id = lr.requestedbyuserid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} au ON au.id = lr.approvedbyuserid
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = lr.leavetypeid
            WHERE lr.id = @Id AND lr.isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<LeaveDetailRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<bool> HasOverlappingApprovedAsync(
        LeaveRequestType type,
        Guid? employeeid,
        Guid? studentId,
        DateOnly from,
        DateOnly to,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(1) FROM {Schema}.{DatabaseConfig.TableLeaveRequests}
            WHERE isactive = true AND requesttype = @RequestType AND status = @Approved
              AND fromdate <= @To AND todate >= @From
              AND (@ExcludeId IS NULL OR id <> @ExcludeId)
              AND (
                (@EmployeeId IS NOT NULL AND employeeid = @EmployeeId)
                OR (@StudentId IS NOT NULL AND studentid = @StudentId)
              );
            """;

        int count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            RequestType = (short)type,
            Approved = (short)LeaveRequestStatus.Approved,
            From = from,
            To = to,
            employeeid = employeeid,
            StudentId = studentId,
            ExcludeId = excludeId
        }, cancellationToken: ct)).ConfigureAwait(false);

        return count > 0;
    }

    public async Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id FROM {Schema}.{DatabaseConfig.TableEmployees}
            WHERE userid = @UserId AND isactive = true LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetClassIdForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT classid FROM {Schema}.{DatabaseConfig.TableStudentAcademics}
            WHERE studentid = @StudentId AND isactive = true
            ORDER BY createdon DESC LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetClassTeacherUserIdAsync(Guid classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT t.userid FROM {Schema}.{DatabaseConfig.TableClassSettings} s
            INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = s.teacherid AND t.isactive = true
            WHERE s.sectionid = @ClassId AND s.isactive = true AND s.teacherid IS NOT NULL
              AND t.userid IS NOT NULL
            LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<Guid>> GetSchoolAdminUserIdsAsync(Guid schoolId, CancellationToken ct = default)
    {
        IList<SchoolAdminUserRow> users = await GetSchoolAdminUsersAsync(schoolId, ct).ConfigureAwait(false);
        return users.Select(u => u.Id).ToList();
    }

    public Task<IList<SchoolAdminUserRow>> GetSchoolAdminUsersAsync(Guid schoolId, CancellationToken ct = default)
    {
        _ = schoolId;
        return GetUsersByUserTypeAsync(UserTypeCodes.Ids.SchoolAdmin, ct);
    }

    public async Task<IList<SchoolAdminUserRow>> GetUsersByUserTypeAsync(Guid userTypeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT u.id AS Id,
                   COALESCE(
                       NULLIF(TRIM(CONCAT_WS(' ', u.firstname, u.lastname)), ''),
                       NULLIF(TRIM(u.username), ''),
                       u.email
                   ) AS Name
            FROM {G}.{DatabaseConfig.TableUsers} u
            WHERE u.isactive = true
              AND u.usertypeid = @UserTypeId
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<SchoolAdminUserRow>(
                new CommandDefinition(sql, new { UserTypeId = userTypeId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public Task<bool> IsParentLinkedToStudentAsync(Guid parentUserId, Guid studentId, CancellationToken ct = default)
    {
        // Parent portal accounts are no longer provisioned; there is no user-linked-to-student data source left.
        return Task.FromResult(false);
    }

    public async Task<IList<Guid>> GetActiveTeacherUserIdsAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT t.userid AS UserId
            FROM {Schema}.{DatabaseConfig.TableEmployees} t
            WHERE t.isactive = true;
            """;
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return ids.Distinct().ToList();
    }

    public async Task<Guid?> GetReportingManagerUserIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        SchoolAdminUserRow? manager = await GetReportingManagerUserAsync(employeeId, ct).ConfigureAwait(false);
        return manager?.Id;
    }

    public async Task<SchoolAdminUserRow?> GetReportingManagerUserAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT mgr.userid AS Id,
                   COALESCE(
                       NULLIF(TRIM(CONCAT_WS(' ', mu.firstname, mu.lastname)), ''),
                       NULLIF(TRIM(mu.username), ''),
                       mu.email
                   ) AS Name
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} mgr
                ON mgr.id = e.reportingmanagerid AND mgr.isactive = true AND mgr.userid IS NOT NULL
            INNER JOIN {G}.{DatabaseConfig.TableUsers} mu
                ON mu.id = mgr.userid AND mu.isactive = true
            WHERE e.id = @EmployeeId AND e.isactive = true
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<SchoolAdminUserRow>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<SchoolAdminUserRow?> GetEmployeeUserByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT e.id AS Id,
                   COALESCE(
                       NULLIF(TRIM(CONCAT_WS(' ', u.firstname, u.lastname)), ''),
                       NULLIF(TRIM(u.username), ''),
                       u.email
                   ) AS Name
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {G}.{DatabaseConfig.TableUsers} u
                ON u.id = e.userid AND u.isactive = true
            WHERE e.userid = @UserId AND e.isactive = true
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<SchoolAdminUserRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task ReplaceHalfDaysAsync(
        Guid leaveRequestId,
        IReadOnlyList<LeaveHalfDayEntity> halfDays,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            DELETE FROM {Schema}.{DatabaseConfig.TableLeaveHalfDays}
            WHERE leaverequestid = @LeaveRequestId;
            """,
            new { LeaveRequestId = leaveRequestId },
            cancellationToken: ct)).ConfigureAwait(false);

        if (halfDays.Count == 0)
        {
            return;
        }

        string insertSql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableLeaveHalfDays}
                (id, leaverequestid, leavedate, session)
            VALUES
                (@Id, @LeaveRequestId, @LeaveDate, @Session);
            """;

        foreach (LeaveHalfDayEntity row in halfDays)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                row.Id,
                row.LeaveRequestId,
                row.LeaveDate,
                Session = (short)row.Session
            }, cancellationToken: ct)).ConfigureAwait(false);
        }
    }

    public async Task<IList<LeaveHalfDayEntity>> GetHalfDaysAsync(Guid leaveRequestId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id,
                   leaverequestid AS LeaveRequestId,
                   leavedate AS LeaveDate,
                   session AS Session
            FROM {Schema}.{DatabaseConfig.TableLeaveHalfDays}
            WHERE leaverequestid = @LeaveRequestId
            ORDER BY leavedate;
            """;
        var rows = await connection.QueryAsync<LeaveHalfDayEntity>(
            new CommandDefinition(sql, new { LeaveRequestId = leaveRequestId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public Task<IList<Guid>> GetParentUserIdsForClassAsync(Guid classId, CancellationToken ct = default)
    {
        // Parent portal accounts are no longer provisioned; there is no parent-to-class data source left.
        return Task.FromResult<IList<Guid>>(Array.Empty<Guid>());
    }

    public Task<IList<LinkedStudentRow>> GetLinkedStudentsForParentAsync(Guid parentUserId, CancellationToken ct = default)
    {
        // Parent portal accounts are no longer provisioned; there are no linked students to return.
        return Task.FromResult<IList<LinkedStudentRow>>(Array.Empty<LinkedStudentRow>());
    }
}
