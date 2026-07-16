using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Audit;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FrontOffice;
using SmartOps.Domain.Modules.FrontOffice.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FrontOffice;

public sealed class FrontOfficeRepository : BaseRepository, IFrontOfficeRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public FrontOfficeRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    // ── Complaint types ──────────────────────────────────────

    public async Task<IList<ComplaintTypeEntity>> GetComplaintTypesAsync(
        string? activeFilter = "All",
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "t", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS Id, t.name AS Name, t.description AS Description, t.displayorder AS DisplayOrder,
                   t.isactive AS IsActive, t.versionno AS VersionNo,
                   t.createdby AS CreatedBy, t.createdon AS CreatedOn, t.updatedby AS UpdatedBy, t.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableComplaintTypes} t
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "t")}{branchFilter}
            ORDER BY t.displayorder ASC, t.name ASC;
            """;
        var rows = await connection.QueryAsync<ComplaintTypeEntity>(
            new CommandDefinition(sql, new { ActiveBranchId = activeBranchId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ComplaintTypeEntity?> GetComplaintTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, description AS Description, displayorder AS DisplayOrder,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableComplaintTypes}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<ComplaintTypeEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateComplaintTypeAsync(ComplaintTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableComplaintTypes}
                (id, branchid, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @Name, @Description, @DisplayOrder, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateComplaintTypeAsync(ComplaintTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableComplaintTypes}
            SET name = @Name, description = @Description, displayorder = @DisplayOrder,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteComplaintTypeAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableComplaintTypes, id).ConfigureAwait(false);
    }

    // ── Visitor purposes ─────────────────────────────────────

    public async Task<IList<VisitorPurposeEntity>> GetVisitorPurposesAsync(
        string? activeFilter = "All",
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "t", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS Id, t.name AS Name, t.description AS Description, t.displayorder AS DisplayOrder,
                   t.isactive AS IsActive, t.versionno AS VersionNo,
                   t.createdby AS CreatedBy, t.createdon AS CreatedOn, t.updatedby AS UpdatedBy, t.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableVisitorPurposes} t
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "t")}{branchFilter}
            ORDER BY t.displayorder ASC, t.name ASC;
            """;
        var rows = await connection.QueryAsync<VisitorPurposeEntity>(
            new CommandDefinition(sql, new { ActiveBranchId = activeBranchId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<VisitorPurposeEntity?> GetVisitorPurposeByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, description AS Description, displayorder AS DisplayOrder,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableVisitorPurposes}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<VisitorPurposeEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateVisitorPurposeAsync(VisitorPurposeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableVisitorPurposes}
                (id, branchid, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @Name, @Description, @DisplayOrder, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVisitorPurposeAsync(VisitorPurposeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableVisitorPurposes}
            SET name = @Name, description = @Description, displayorder = @DisplayOrder,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteVisitorPurposeAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableVisitorPurposes, id).ConfigureAwait(false);
    }

    // ── Visitors ─────────────────────────────────────────────

    public async Task<IList<VisitorListRow>> GetVisitorsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = new StringBuilder($"""
            SELECT v.id AS Id, v.name AS Name, v.phone AS Phone, v.idcardtype AS IdCardType,
                   v.idcardnumber AS IdCardNumber, v.purposeid AS PurposeId, p.name AS PurposeName,
                   v.meetingwith AS MeetingWith, v.intime AS InTime, v.outtime AS OutTime,
                   v.note AS Note, v.documentpath AS DocumentPath, v.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableVisitors} v
            LEFT JOIN {Schema}.{DatabaseConfig.TableVisitorPurposes} p ON p.id = v.purposeid
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "v")}
            """);
        var parameters = new DynamicParameters();
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, sql, parameters, "v", ct)
            .ConfigureAwait(false);
        AppendDateRangeFilter(sql, parameters, "v.intime::date", fromDate, toDate);
        sql.Append(" ORDER BY v.intime DESC;");
        var rows = await connection.QueryAsync<VisitorListRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<VisitorListRow?> GetVisitorByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT v.id AS Id, v.name AS Name, v.phone AS Phone, v.idcardtype AS IdCardType,
                   v.idcardnumber AS IdCardNumber, v.purposeid AS PurposeId, p.name AS PurposeName,
                   v.meetingwith AS MeetingWith, v.intime AS InTime, v.outtime AS OutTime,
                   v.note AS Note, v.documentpath AS DocumentPath, v.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableVisitors} v
            LEFT JOIN {Schema}.{DatabaseConfig.TableVisitorPurposes} p ON p.id = v.purposeid
            WHERE v.id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<VisitorListRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateVisitorAsync(VisitorEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);
        await InsertAsync(connection, Schema, DatabaseConfig.TableVisitors, entity)
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVisitorAsync(VisitorEntity patch, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        VisitorEntity? entity = await GetVisitorEntityByIdAsync(patch.Id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.Name = patch.Name;
        entity.Phone = patch.Phone;
        entity.IdCardType = patch.IdCardType;
        entity.IdCardNumber = patch.IdCardNumber;
        entity.PurposeId = patch.PurposeId;
        entity.MeetingWith = patch.MeetingWith;
        entity.InTime = patch.InTime;
        entity.OutTime = patch.OutTime;
        entity.Note = patch.Note;
        entity.DocumentPath = patch.DocumentPath;
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        await UpdateAsync(connection, Schema, DatabaseConfig.TableVisitors, entity, null, "Id")
            .ConfigureAwait(false);
    }

    public async Task SoftDeleteVisitorAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableVisitors, id).ConfigureAwait(false);
    }

    public async Task CheckoutVisitorAsync(Guid id, DateTimeOffset outTime, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actor = ResolveUpdateActor();
        DateTime now = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableVisitors}
            SET outtime = @OutTime, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, OutTime = outTime, Actor = actor, Now = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    // ── Phone logs ───────────────────────────────────────────

    public async Task<IList<PhoneLogEntity>> GetPhoneLogsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = new StringBuilder($"""
            SELECT pl.id AS Id, pl.callername AS CallerName, pl.phone AS Phone, pl.calltype AS CallType,
                   pl.calldate AS CallDate, pl.duration AS Duration, pl.description AS Description,
                   pl.nextfollowupdate AS NextFollowUpDate, pl.note AS Note,
                   pl.isactive AS IsActive, pl.versionno AS VersionNo,
                   pl.createdby AS CreatedBy, pl.createdon AS CreatedOn, pl.updatedby AS UpdatedBy, pl.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePhoneLogs} pl
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "pl")}
            """);
        var parameters = new DynamicParameters();
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, sql, parameters, "pl", ct)
            .ConfigureAwait(false);
        AppendDateRangeFilter(sql, parameters, "pl.calldate", fromDate, toDate);
        sql.Append(" ORDER BY pl.calldate DESC, pl.createdon DESC;");
        var rows = await connection.QueryAsync<PhoneLogEntity>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<PhoneLogEntity?> GetPhoneLogByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, callername AS CallerName, phone AS Phone, calltype AS CallType,
                   calldate AS CallDate, duration AS Duration, description AS Description,
                   nextfollowupdate AS NextFollowUpDate, note AS Note,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePhoneLogs}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<PhoneLogEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreatePhoneLogAsync(PhoneLogEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);
        await InsertAsync(connection, Schema, DatabaseConfig.TablePhoneLogs, entity)
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdatePhoneLogAsync(PhoneLogEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        await UpdateAsync(connection, Schema, DatabaseConfig.TablePhoneLogs, entity, null, "Id")
            .ConfigureAwait(false);
    }

    public async Task SoftDeletePhoneLogAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TablePhoneLogs, id).ConfigureAwait(false);
    }

    // ── Complaints ───────────────────────────────────────────

    public async Task<IList<ComplaintListRow>> GetComplaintsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = new StringBuilder($"""
            SELECT c.id AS Id, c.complainttypeid AS ComplaintTypeId, t.name AS ComplaintTypeName,
                   c.complaintdate AS ComplaintDate, c.isanonymous AS IsAnonymous,
                   c.complainantname AS ComplainantName, c.phone AS Phone, c.description AS Description,
                   c.assignedtoemployeeid AS AssignedToEmployeeId,
                   TRIM(e.firstname || ' ' || e.lastname) AS AssignedToEmployeeName,
                   c.status AS Status, c.actiontaken AS ActionTaken, c.note AS Note,
                   c.documentpath AS DocumentPath, c.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableComplaints} c
            LEFT JOIN {Schema}.{DatabaseConfig.TableComplaintTypes} t ON t.id = c.complainttypeid
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = c.assignedtoemployeeid
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "c")}
            """);
        var parameters = new DynamicParameters();
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, sql, parameters, "c", ct)
            .ConfigureAwait(false);
        AppendDateRangeFilter(sql, parameters, "c.complaintdate", fromDate, toDate);
        AppendStatusFilter(sql, parameters, "c.status", status);
        sql.Append(" ORDER BY c.complaintdate DESC, c.createdon DESC;");
        var rows = await connection.QueryAsync<ComplaintListRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ComplaintListRow?> GetComplaintByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT c.id AS Id, c.complainttypeid AS ComplaintTypeId, t.name AS ComplaintTypeName,
                   c.complaintdate AS ComplaintDate, c.isanonymous AS IsAnonymous,
                   c.complainantname AS ComplainantName, c.phone AS Phone, c.description AS Description,
                   c.assignedtoemployeeid AS AssignedToEmployeeId,
                   TRIM(e.firstname || ' ' || e.lastname) AS AssignedToEmployeeName,
                   c.status AS Status, c.actiontaken AS ActionTaken, c.note AS Note,
                   c.documentpath AS DocumentPath, c.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableComplaints} c
            LEFT JOIN {Schema}.{DatabaseConfig.TableComplaintTypes} t ON t.id = c.complainttypeid
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = c.assignedtoemployeeid
            WHERE c.id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<ComplaintListRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateComplaintAsync(ComplaintEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);
        await InsertAsync(connection, Schema, DatabaseConfig.TableComplaints, entity)
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateComplaintAsync(ComplaintEntity patch, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ComplaintEntity? entity = await GetComplaintEntityByIdAsync(patch.Id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.ComplaintTypeId = patch.ComplaintTypeId;
        entity.ComplaintDate = patch.ComplaintDate;
        entity.IsAnonymous = patch.IsAnonymous;
        entity.ComplainantName = patch.ComplainantName;
        entity.Phone = patch.Phone;
        entity.Description = patch.Description;
        entity.AssignedToEmployeeId = patch.AssignedToEmployeeId;
        entity.Status = patch.Status;
        entity.ActionTaken = patch.ActionTaken;
        entity.Note = patch.Note;
        entity.DocumentPath = patch.DocumentPath;
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        await UpdateAsync(connection, Schema, DatabaseConfig.TableComplaints, entity, null, "Id")
            .ConfigureAwait(false);
    }

    public async Task SoftDeleteComplaintAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableComplaints, id).ConfigureAwait(false);
    }

    // ── Admission inquiries ──────────────────────────────────

    public async Task<IList<AdmissionInquiryListRow>> GetAdmissionInquiriesAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = new StringBuilder($"""
            SELECT a.id AS Id, a.parentname AS ParentName, a.phone AS Phone, a.whatsapp AS WhatsApp,
                   a.email AS Email, a.address AS Address, a.studentname AS StudentName, a.classlabel AS ClassLabel,
                   a.inquirydate AS InquiryDate, a.nextfollowupdate AS NextFollowUpDate,
                   a.assignedtoemployeeid AS AssignedToEmployeeId,
                   TRIM(e.firstname || ' ' || e.lastname) AS AssignedToEmployeeName,
                   a.reference AS Reference, a.status AS Status, a.description AS Description,
                   a.autofollowup AS AutoFollowUp, a.streamgroup AS StreamGroup, a.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableAdmissionInquiries} a
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = a.assignedtoemployeeid
            WHERE 1 = 1{BuildIsActiveClause(activeFilter, "a")}
            """);
        var parameters = new DynamicParameters();
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, sql, parameters, "a", ct)
            .ConfigureAwait(false);
        AppendDateRangeFilter(sql, parameters, "a.inquirydate", fromDate, toDate);
        AppendStatusFilter(sql, parameters, "a.status", status);
        sql.Append(" ORDER BY a.inquirydate DESC, a.createdon DESC;");
        var rows = await connection.QueryAsync<AdmissionInquiryListRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<AdmissionInquiryListRow?> GetAdmissionInquiryByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT a.id AS Id, a.parentname AS ParentName, a.phone AS Phone, a.whatsapp AS WhatsApp,
                   a.email AS Email, a.address AS Address, a.studentname AS StudentName, a.classlabel AS ClassLabel,
                   a.inquirydate AS InquiryDate, a.nextfollowupdate AS NextFollowUpDate,
                   a.assignedtoemployeeid AS AssignedToEmployeeId,
                   TRIM(e.firstname || ' ' || e.lastname) AS AssignedToEmployeeName,
                   a.reference AS Reference, a.status AS Status, a.description AS Description,
                   a.autofollowup AS AutoFollowUp, a.streamgroup AS StreamGroup, a.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableAdmissionInquiries} a
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = a.assignedtoemployeeid
            WHERE a.id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<AdmissionInquiryListRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateAdmissionInquiryAsync(AdmissionInquiryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);
        await InsertAsync(connection, Schema, DatabaseConfig.TableAdmissionInquiries, entity)
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAdmissionInquiryAsync(AdmissionInquiryEntity patch, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        AdmissionInquiryEntity? entity = await GetAdmissionInquiryEntityByIdAsync(patch.Id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.ParentName = patch.ParentName;
        entity.Phone = patch.Phone;
        entity.WhatsApp = patch.WhatsApp;
        entity.Email = patch.Email;
        entity.Address = patch.Address;
        entity.StudentName = patch.StudentName;
        entity.ClassLabel = patch.ClassLabel;
        entity.InquiryDate = patch.InquiryDate;
        entity.NextFollowUpDate = patch.NextFollowUpDate;
        entity.AssignedToEmployeeId = patch.AssignedToEmployeeId;
        entity.Reference = patch.Reference;
        entity.Status = patch.Status;
        entity.Description = patch.Description;
        entity.AutoFollowUp = patch.AutoFollowUp;
        entity.StreamGroup = patch.StreamGroup;
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        await UpdateAsync(connection, Schema, DatabaseConfig.TableAdmissionInquiries, entity, null, "Id")
            .ConfigureAwait(false);
    }

    public async Task SoftDeleteAdmissionInquiryAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableAdmissionInquiries, id).ConfigureAwait(false);
    }

    public async Task ConvertAdmissionInquiryAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actor = ResolveUpdateActor();
        DateTime now = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableAdmissionInquiries}
            SET status = @Status, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            Status = (short)InquiryStatus.Enrolled,
            Actor = actor,
            Now = now
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    // ── Lookups ──────────────────────────────────────────────

    public async Task<IReadOnlyList<DropdownDto>> GetActiveEmployeesAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "e", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT e.id AS Id, TRIM(e.firstname || ' ' || e.lastname) AS Name
            FROM {Schema}.{DatabaseConfig.TableEmployees} e
            WHERE e.isactive = true{branchFilter}
            ORDER BY e.firstname ASC, e.lastname ASC;
            """;
        var items = await connection.QueryAsync<DropdownDto>(
            new CommandDefinition(sql, new { ActiveBranchId = activeBranchId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return items.ToList();
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string BuildIsActiveClause(string? activeFilter, string? tableAlias = null)
    {
        string column = string.IsNullOrEmpty(tableAlias) ? "isactive" : $"{tableAlias}.isactive";
        return (activeFilter ?? "All").Trim().ToLowerInvariant() switch
        {
            "active" => $" AND {column} = true",
            "inactive" => $" AND {column} = false",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Append date filters only when values exist — avoids Npgsql 42P08 on null DateOnly params.
    /// </summary>
    private static void AppendDateRangeFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string dateExpression,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue)
        {
            sql.Append($" AND {dateExpression} >= @FromDate");
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sql.Append($" AND {dateExpression} <= @ToDate");
            parameters.Add("ToDate", toDate.Value);
        }
    }

    private static void AppendStatusFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string statusExpression,
        int? status)
    {
        if (!status.HasValue)
        {
            return;
        }

        sql.Append($" AND {statusExpression} = @Status");
        parameters.Add("Status", (short)status.Value);
    }

    private async Task<VisitorEntity?> GetVisitorEntityByIdAsync(Guid id, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, phone AS Phone, idcardtype AS IdCardType,
                   idcardnumber AS IdCardNumber, purposeid AS PurposeId, meetingwith AS MeetingWith,
                   intime AS InTime, outtime AS OutTime, note AS Note, documentpath AS DocumentPath,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableVisitors}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<VisitorEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private async Task<ComplaintEntity?> GetComplaintEntityByIdAsync(Guid id, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, complainttypeid AS ComplaintTypeId, complaintdate AS ComplaintDate,
                   isanonymous AS IsAnonymous, complainantname AS ComplainantName, phone AS Phone,
                   description AS Description, assignedtoemployeeid AS AssignedToEmployeeId,
                   status AS Status, actiontaken AS ActionTaken, note AS Note, documentpath AS DocumentPath,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableComplaints}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<ComplaintEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private async Task<AdmissionInquiryEntity?> GetAdmissionInquiryEntityByIdAsync(Guid id, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, parentname AS ParentName, phone AS Phone, whatsapp AS WhatsApp,
                   email AS Email, address AS Address, studentname AS StudentName, classlabel AS ClassLabel,
                   inquirydate AS InquiryDate, nextfollowupdate AS NextFollowUpDate,
                   assignedtoemployeeid AS AssignedToEmployeeId, reference AS Reference, status AS Status,
                   description AS Description, autofollowup AS AutoFollowUp, streamgroup AS StreamGroup,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableAdmissionInquiries}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<AdmissionInquiryEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }
}
