using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Audit;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FrontOffice;
using SmartOps.Domain.Modules.FrontOffice.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FrontOffice;

public sealed class FrontOfficeRepository : BaseRepository, IFrontOfficeRepository
{
    private static readonly FieldChangeDto[] EmptyFieldChanges = [];

    private readonly ITenantSchemaProvider _tenantSchema;

    public FrontOfficeRepository(
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

    // ── Complaint types ──────────────────────────────────────

    public async Task<IList<ComplaintTypeEntity>> GetComplaintTypesAsync(
        string? activeFilter = "All",
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, description AS Description, displayorder AS DisplayOrder,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableComplaintTypes}
            WHERE 1 = 1{BuildIsActiveClause(activeFilter)}
            ORDER BY displayorder ASC, name ASC;
            """;
        var rows = await connection.QueryAsync<ComplaintTypeEntity>(new CommandDefinition(sql, cancellationToken: ct))
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableComplaintTypes}
                (id, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Name, @Description, @DisplayOrder, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
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
        string sql = $"""
            SELECT id AS Id, name AS Name, description AS Description, displayorder AS DisplayOrder,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableVisitorPurposes}
            WHERE 1 = 1{BuildIsActiveClause(activeFilter)}
            ORDER BY displayorder ASC, name ASC;
            """;
        var rows = await connection.QueryAsync<VisitorPurposeEntity>(new CommandDefinition(sql, cancellationToken: ct))
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableVisitorPurposes}
                (id, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Name, @Description, @DisplayOrder, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableVisitors}
                (id, name, phone, idcardtype, idcardnumber, purposeid, meetingwith, intime, outtime, note, documentpath,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Name, @Phone, @IdCardType, @IdCardNumber, @PurposeId, @MeetingWith, @InTime, @OutTime, @Note, @DocumentPath,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableVisitors, entity.Id,
            "Created", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVisitorAsync(VisitorEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableVisitors}
            SET name = @Name, phone = @Phone, idcardtype = @IdCardType, idcardnumber = @IdCardNumber,
                purposeid = @PurposeId, meetingwith = @MeetingWith, intime = @InTime, outtime = @OutTime,
                note = @Note, documentpath = @DocumentPath,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableVisitors, entity.Id,
            "Updated", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
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
            SELECT id AS Id, callername AS CallerName, phone AS Phone, calltype AS CallType,
                   calldate AS CallDate, duration AS Duration, description AS Description,
                   nextfollowupdate AS NextFollowUpDate, note AS Note,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePhoneLogs}
            WHERE 1 = 1{BuildIsActiveClause(activeFilter)}
            """);
        var parameters = new DynamicParameters();
        AppendDateRangeFilter(sql, parameters, "calldate", fromDate, toDate);
        sql.Append(" ORDER BY calldate DESC, createdon DESC;");
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePhoneLogs}
                (id, callername, phone, calltype, calldate, duration, description, nextfollowupdate, note,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @CallerName, @Phone, @CallType, @CallDate, @Duration, @Description, @NextFollowUpDate, @Note,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapPhoneLog(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TablePhoneLogs, entity.Id,
            "Created", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdatePhoneLogAsync(PhoneLogEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TablePhoneLogs}
            SET callername = @CallerName, phone = @Phone, calltype = @CallType, calldate = @CallDate,
                duration = @Duration, description = @Description, nextfollowupdate = @NextFollowUpDate, note = @Note,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapPhoneLog(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TablePhoneLogs, entity.Id,
            "Updated", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableComplaints}
                (id, complainttypeid, complaintdate, isanonymous, complainantname, phone, description,
                 assignedtoemployeeid, status, actiontaken, note, documentpath,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ComplaintTypeId, @ComplaintDate, @IsAnonymous, @ComplainantName, @Phone, @Description,
                 @AssignedToEmployeeId, @Status, @ActionTaken, @Note, @DocumentPath,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapComplaint(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableComplaints, entity.Id,
            "Created", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateComplaintAsync(ComplaintEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableComplaints}
            SET complainttypeid = @ComplaintTypeId, complaintdate = @ComplaintDate, isanonymous = @IsAnonymous,
                complainantname = @ComplainantName, phone = @Phone, description = @Description,
                assignedtoemployeeid = @AssignedToEmployeeId, status = @Status, actiontaken = @ActionTaken,
                note = @Note, documentpath = @DocumentPath,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapComplaint(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableComplaints, entity.Id,
            "Updated", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
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
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableAdmissionInquiries}
                (id, parentname, phone, whatsapp, email, address, studentname, classlabel, inquirydate,
                 nextfollowupdate, assignedtoemployeeid, reference, status, description, autofollowup, streamgroup,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ParentName, @Phone, @WhatsApp, @Email, @Address, @StudentName, @ClassLabel, @InquiryDate,
                 @NextFollowUpDate, @AssignedToEmployeeId, @Reference, @Status, @Description, @AutoFollowUp, @StreamGroup,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapInquiry(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableAdmissionInquiries, entity.Id,
            "Created", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAdmissionInquiryAsync(AdmissionInquiryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableAdmissionInquiries}
            SET parentname = @ParentName, phone = @Phone, whatsapp = @WhatsApp, email = @Email,
                address = @Address, studentname = @StudentName, classlabel = @ClassLabel,
                inquirydate = @InquiryDate, nextfollowupdate = @NextFollowUpDate,
                assignedtoemployeeid = @AssignedToEmployeeId, reference = @Reference, status = @Status,
                description = @Description, autofollowup = @AutoFollowUp, streamgroup = @StreamGroup,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, MapInquiry(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        await WriteAuditLogInternalAsync(
            connection, Schema, DatabaseConfig.TableAdmissionInquiries, entity.Id,
            "Updated", actorId, utcNow, EmptyFieldChanges).ConfigureAwait(false);
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
        string sql = $"""
            SELECT id AS Id, TRIM(firstname || ' ' || lastname) AS Name
            FROM {Schema}.{DatabaseConfig.TableEmployees}
            WHERE isactive = true
            ORDER BY firstname ASC, lastname ASC;
            """;
        var items = await connection.QueryAsync<DropdownDto>(new CommandDefinition(sql, cancellationToken: ct))
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

    private static object MapPhoneLog(PhoneLogEntity entity) => new
    {
        entity.Id,
        entity.CallerName,
        entity.Phone,
        CallType = (short)entity.CallType,
        entity.CallDate,
        entity.Duration,
        entity.Description,
        entity.NextFollowUpDate,
        entity.Note,
        entity.IsActive,
        entity.VersionNo,
        entity.CreatedBy,
        entity.CreatedOn,
        entity.UpdatedBy,
        entity.UpdatedOn
    };

    private static object MapComplaint(ComplaintEntity entity) => new
    {
        entity.Id,
        entity.ComplaintTypeId,
        entity.ComplaintDate,
        entity.IsAnonymous,
        entity.ComplainantName,
        entity.Phone,
        entity.Description,
        entity.AssignedToEmployeeId,
        Status = (short)entity.Status,
        entity.ActionTaken,
        entity.Note,
        entity.DocumentPath,
        entity.IsActive,
        entity.VersionNo,
        entity.CreatedBy,
        entity.CreatedOn,
        entity.UpdatedBy,
        entity.UpdatedOn
    };

    private static object MapInquiry(AdmissionInquiryEntity entity) => new
    {
        entity.Id,
        entity.ParentName,
        entity.Phone,
        entity.WhatsApp,
        entity.Email,
        entity.Address,
        entity.StudentName,
        entity.ClassLabel,
        entity.InquiryDate,
        entity.NextFollowUpDate,
        entity.AssignedToEmployeeId,
        entity.Reference,
        Status = (short)entity.Status,
        entity.Description,
        entity.AutoFollowUp,
        entity.StreamGroup,
        entity.IsActive,
        entity.VersionNo,
        entity.CreatedBy,
        entity.CreatedOn,
        entity.UpdatedBy,
        entity.UpdatedOn
    };
}
