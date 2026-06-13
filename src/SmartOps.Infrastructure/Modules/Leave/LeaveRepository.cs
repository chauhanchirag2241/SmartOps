using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Configuration;
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

    private static string G => DatabaseConfig.Schema_Global;

    private const string ClassDisplayNameSql =
        "cl.classname || CASE cl.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    public async Task<Guid> CreateAsync(LeaveRequestEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableLeaveRequests}
                (id, requesttype, employeeid, studentid, requestedbyuserid, fromdate, todate,
                 leavetype, reason, status, approvedbyuserid, approvedon, approverremark,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @RequestType, @EmployeeId, @StudentId, @RequestedByUserId, @FromDate, @ToDate,
                 @LeaveType, @Reason, @Status, @ApprovedByUserId, @ApprovedOn, @ApproverRemark,
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
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);

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
                   leavetype AS LeaveType, reason AS Reason, status AS Status,
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
                   t.firstname AS TeacherFirstName, t.lastname AS TeacherLastName,
                   lr.studentid AS StudentId, s.firstname AS StudentFirstName, s.lastname AS StudentLastName,
                   {ClassDisplayNameSql} AS ClassName,
                   lr.requestedbyuserid AS RequestedByUserId, u.email AS RequestedByEmail,
                   lr.fromdate AS FromDate, lr.todate AS ToDate, lr.leavetype AS LeaveType,
                   lr.status AS Status, lr.createdon AS CreatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveRequests} lr
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = lr.employeeid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = lr.studentid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = sa.classid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = lr.requestedbyuserid
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
                   t.firstname AS TeacherFirstName, t.lastname AS TeacherLastName,
                   lr.studentid AS StudentId, s.firstname AS StudentFirstName, s.lastname AS StudentLastName,
                   {ClassDisplayNameSql} AS ClassName,
                   lr.requestedbyuserid AS RequestedByUserId, ru.email AS RequestedByEmail,
                   lr.fromdate AS FromDate, lr.todate AS ToDate, lr.leavetype AS LeaveType,
                   lr.status AS Status, lr.reason AS Reason,
                   lr.approvedbyuserid AS ApprovedByUserId, au.email AS ApprovedByEmail,
                   lr.approvedon AS ApprovedOn, lr.approverremark AS ApproverRemark,
                   lr.createdon AS CreatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveRequests} lr
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = lr.employeeid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = lr.studentid
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = sa.classid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} ru ON ru.id = lr.requestedbyuserid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} au ON au.id = lr.approvedbyuserid
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
            SELECT t.userid FROM {Schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
            INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid AND t.isactive = true
            WHERE m.classid = @ClassId AND m.isclassteacher = true AND m.isactive = true
              AND t.userid IS NOT NULL
            ORDER BY m.createdon DESC LIMIT 1;
            """;
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<Guid>> GetSchoolAdminUserIdsAsync(Guid schoolId, CancellationToken ct = default)
    {
        IList<SchoolAdminUserRow> users = await GetSchoolAdminUsersAsync(schoolId, ct).ConfigureAwait(false);
        return users.Select(u => u.Id).ToList();
    }

    public async Task<IList<SchoolAdminUserRow>> GetSchoolAdminUsersAsync(Guid schoolId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT u.id AS Id,
                   COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS Name
            FROM {G}.{DatabaseConfig.TableUsers} u
            INNER JOIN {G}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id AND ur.isactive = true
            INNER JOIN {G}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid AND r.isactive = true
            INNER JOIN {G}.{DatabaseConfig.TableUserSchoolMappings} usm ON usm.userid = u.id AND usm.isactive = true
            WHERE u.isactive = true AND usm.schoolid = @SchoolId
              AND (r.code = 'SCHOOL_ADMIN' OR r.code = 'ADMIN')
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<SchoolAdminUserRow>(new CommandDefinition(sql, new { SchoolId = schoolId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> IsParentLinkedToStudentAsync(Guid parentUserId, Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(1) FROM {Schema}.{DatabaseConfig.TableParentStudentMappings}
            WHERE parentuserid = @ParentUserId AND studentid = @StudentId AND isactive = true;
            """;
        int count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { ParentUserId = parentUserId, StudentId = studentId }, cancellationToken: ct)).ConfigureAwait(false);
        if (count > 0)
        {
            return true;
        }

        string sql2 = $"""
            SELECT COUNT(1) FROM {Schema}.{DatabaseConfig.TableStudentParents}
            WHERE userid = @ParentUserId AND studentid = @StudentId AND isactive = true;
            """;
        count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql2, new { ParentUserId = parentUserId, StudentId = studentId }, cancellationToken: ct)).ConfigureAwait(false);
        return count > 0;
    }

    public async Task<IList<Guid>> GetActiveTeacherUserIdsAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT COALESCE(t.userid, u.id) AS UserId
            FROM {Schema}.{DatabaseConfig.TableEmployees} t
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u
                ON u.isactive = true
               AND t.userid IS NULL
               AND t.email IS NOT NULL
               AND lower(trim(u.email)) = lower(trim(t.email))
            WHERE t.isactive = true
              AND COALESCE(t.userid, u.id) IS NOT NULL;
            """;
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return ids.Distinct().ToList();
    }

    public async Task<IList<Guid>> GetParentUserIdsForClassAsync(Guid classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT psm.parentuserid
            FROM {Schema}.{DatabaseConfig.TableParentStudentMappings} psm
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                ON sa.studentid = psm.studentid AND sa.isactive = true AND sa.classid = @ClassId
            WHERE psm.isactive = true
            UNION
            SELECT DISTINCT sp.userid
            FROM {Schema}.{DatabaseConfig.TableStudentParents} sp
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                ON sa.studentid = sp.studentid AND sa.isactive = true AND sa.classid = @ClassId
            WHERE sp.isactive = true AND sp.userid IS NOT NULL;
            """;
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return ids.Distinct().ToList();
    }

    public async Task<IList<LinkedStudentRow>> GetLinkedStudentsForParentAsync(Guid parentUserId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT s.id AS Id, s.firstname AS FirstName, s.lastname AS LastName,
                   {ClassDisplayNameSql} AS ClassName
            FROM {Schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {Schema}.{DatabaseConfig.TableParentStudentMappings} psm
                ON psm.studentid = s.id AND psm.parentuserid = @ParentUserId AND psm.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = sa.classid
            WHERE s.isactive = true
            UNION
            SELECT DISTINCT s.id, s.firstname, s.lastname, {ClassDisplayNameSql}
            FROM {Schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentParents} sp
                ON sp.studentid = s.id AND sp.userid = @ParentUserId AND sp.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} cl ON cl.id = sa.classid
            WHERE s.isactive = true;
            """;

        var rows = await connection.QueryAsync<LinkedStudentRow>(
            new CommandDefinition(sql, new { ParentUserId = parentUserId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }
}
