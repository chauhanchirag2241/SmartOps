using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Notice.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Notice.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Notice;

public sealed class NoticeRepository : BaseRepository, INoticeRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public NoticeRepository(
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

    public async Task<Guid> CreateAsync(NoticeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableNotices}
                (id, title, body, createdbyuserid, publishedon, requiresresponse, responsedeadline,
                 targettype, targetrefid, contenttype, contentjson, status,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Title, @Body, @CreatedByUserId, @PublishedOn, @RequiresResponse, @ResponseDeadline,
                 @TargetType, @TargetRefId, @ContentType, @ContentJson, @Status,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, Map(entity), cancellationToken: ct))
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAsync(NoticeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableNotices}
            SET title = @Title, body = @Body, requiresresponse = @RequiresResponse,
                responsedeadline = @ResponseDeadline, targettype = @TargetType, targetrefid = @TargetRefId,
                contenttype = @ContentType, contentjson = @ContentJson,
                status = @Status, publishedon = @PublishedOn,
                updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, Map(entity), cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<NoticeEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, title AS Title, body AS Body, createdbyuserid AS CreatedByUserId,
                   publishedon AS PublishedOn, requiresresponse AS RequiresResponse,
                   responsedeadline AS ResponseDeadline, targettype AS TargetType, targetrefid AS TargetRefId,
                   contenttype AS ContentType, contentjson AS ContentJson,
                   status AS Status, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableNotices}
            WHERE id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<NoticeEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<NoticeListRow>> GetListAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT n.id AS Id, n.title AS Title, n.status AS Status, n.targettype AS TargetType,
                   n.contenttype AS ContentType, n.isactive AS IsActive,
                   n.requiresresponse AS RequiresResponse, n.responsedeadline AS ResponseDeadline,
                   n.publishedon AS PublishedOn,
                   (SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableNoticeResponses} r
                    WHERE r.noticeid = n.id AND r.isactive = true) AS ResponseCount
            FROM {Schema}.{DatabaseConfig.TableNotices} n
            WHERE n.isactive = true
            ORDER BY n.createdon DESC;
            """;

        var rows = await connection.QueryAsync<NoticeListRow>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task UpsertResponseAsync(Guid noticeId, Guid respondentUserId, string responseBody, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime now = DateTime.UtcNow;

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableNoticeResponses}
                (id, noticeid, respondentuserid, responsebody, respondedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (gen_random_uuid(), @NoticeId, @RespondentUserId, @ResponseBody, @RespondedOn,
                 true, 1, @ActorId, @Now, @ActorId, @Now)
            ON CONFLICT (noticeid, respondentuserid)
            DO UPDATE SET responsebody = EXCLUDED.responsebody, respondedon = EXCLUDED.respondedon,
                updatedby = @ActorId, updatedon = @Now, versionno = {Schema}.{DatabaseConfig.TableNoticeResponses}.versionno + 1;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            NoticeId = noticeId,
            RespondentUserId = respondentUserId,
            ResponseBody = responseBody,
            RespondedOn = DateTimeOffset.UtcNow,
            ActorId = actorId,
            Now = now
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<NoticeResponseRow>> GetResponsesAsync(Guid noticeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT r.id AS Id, r.respondentuserid AS RespondentUserId, u.email AS RespondentEmail,
                   r.responsebody AS ResponseBody, r.respondedon AS RespondedOn
            FROM {Schema}.{DatabaseConfig.TableNoticeResponses} r
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = r.respondentuserid
            WHERE r.noticeid = @NoticeId AND r.isactive = true
            ORDER BY r.respondedon DESC;
            """;

        var rows = await connection.QueryAsync<NoticeResponseRow>(new CommandDefinition(sql, new { NoticeId = noticeId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<int> CountResponsesAsync(Guid noticeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableNoticeResponses}
            WHERE noticeid = @NoticeId AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { NoticeId = noticeId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<NoticeAudienceRow>> GetTeacherAudienceAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT COALESCE(t.userid, u.id) AS Id,
                   TRIM(COALESCE(t.firstname, '') || ' ' || COALESCE(t.lastname, '')) AS Name,
                   t.email AS Subtitle
            FROM {Schema}.{DatabaseConfig.TableTeachers} t
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u
                ON u.isactive = true
               AND t.userid IS NULL
               AND t.email IS NOT NULL
               AND lower(trim(u.email)) = lower(trim(t.email))
            WHERE t.isactive = true
              AND COALESCE(t.userid, u.id) IS NOT NULL
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<NoticeAudienceRow>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<NoticeAudienceRow>> GetParentAudienceAsync(Guid? classId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string classFilter = classId.HasValue ? "AND sa.classid = @ClassId" : string.Empty;
        string sql = $"""
            SELECT DISTINCT p.Id, p.Name, p.Subtitle
            FROM (
                SELECT psm.parentuserid AS Id,
                       COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS Name,
                       STRING_AGG(DISTINCT TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')), ', ') AS Subtitle
                FROM {Schema}.{DatabaseConfig.TableParentStudentMappings} psm
                INNER JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = psm.parentuserid AND u.isactive = true
                INNER JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = psm.studentid AND s.isactive = true
                INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
                WHERE psm.isactive = true {classFilter}
                GROUP BY psm.parentuserid, u.username, u.email
                UNION
                SELECT sp.userid AS Id,
                       COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS Name,
                       STRING_AGG(DISTINCT TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')), ', ') AS Subtitle
                FROM {Schema}.{DatabaseConfig.TableStudentParents} sp
                INNER JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = sp.userid AND u.isactive = true
                INNER JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = sp.studentid AND s.isactive = true
                INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
                WHERE sp.isactive = true AND sp.userid IS NOT NULL {classFilter}
                GROUP BY sp.userid, u.username, u.email
            ) p
            ORDER BY p.Name;
            """;
        var rows = await connection.QueryAsync<NoticeAudienceRow>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<NoticeAudienceRow>> GetSchoolUserAudienceAsync(Guid schoolId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT u.id AS Id,
                   COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS Name,
                   u.email AS Subtitle
            FROM {G}.{DatabaseConfig.TableUsers} u
            INNER JOIN {G}.{DatabaseConfig.TableUserSchoolMappings} usm ON usm.userid = u.id AND usm.isactive = true
            WHERE u.isactive = true AND usm.schoolid = @SchoolId
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<NoticeAudienceRow>(
            new CommandDefinition(sql, new { SchoolId = schoolId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<NoticeFeeParentRow>> GetPendingFeeParentTargetsAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            WITH pending_students AS (
                SELECT s.id AS studentid,
                       TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS studentname,
                       GREATEST(
                           COALESCE(fee_totals.total_fees, 0) - COALESCE(paid_totals.paid, 0),
                           0) AS pendingamount
                FROM {Schema}.{DatabaseConfig.TableStudents} s
                INNER JOIN (
                    SELECT sa.studentid, sa.classid, sa.feestructureversionid,
                           ROW_NUMBER() OVER (PARTITION BY sa.studentid ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                    FROM {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                    WHERE sa.academicyearid = @AcademicYearId
                ) sa ON sa.studentid = s.id AND sa.rn = 1
                LEFT JOIN LATERAL (
                    SELECT COALESCE(
                        NULLIF((SELECT SUM(sfi.amount) FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                                WHERE sfi.studentid = s.id AND sfi.feestructureversionid = sa.feestructureversionid AND sfi.isactive = true), 0),
                        (SELECT SUM(cfi.amount) FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                         WHERE cfi.classid = sa.classid AND cfi.feestructureversionid = sa.feestructureversionid AND cfi.isactive = true),
                        0) AS total_fees
                ) fee_totals ON true
                LEFT JOIN LATERAL (
                    SELECT SUM(fp.amount) AS paid
                    FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                    WHERE fp.studentid = s.id AND fp.feestructureversionid = sa.feestructureversionid AND fp.isactive = true
                ) paid_totals ON true
                WHERE s.isactive = true
            ),
            parent_links AS (
                SELECT psm.parentuserid, ps.studentname, ps.pendingamount
                FROM {Schema}.{DatabaseConfig.TableParentStudentMappings} psm
                INNER JOIN pending_students ps ON ps.studentid = psm.studentid
                WHERE psm.isactive = true AND ps.pendingamount > 0
                UNION ALL
                SELECT sp.userid, ps.studentname, ps.pendingamount
                FROM {Schema}.{DatabaseConfig.TableStudentParents} sp
                INNER JOIN pending_students ps ON ps.studentid = sp.studentid
                WHERE sp.isactive = true AND sp.userid IS NOT NULL AND ps.pendingamount > 0
            )
            SELECT pl.parentuserid AS ParentUserId,
                   COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS ParentName,
                   SUM(pl.pendingamount) AS PendingAmount,
                   STRING_AGG(DISTINCT pl.studentname || ' (₹' || TRIM(TO_CHAR(pl.pendingamount, '999999999.99')) || ')', ', ') AS StudentSummary
            FROM parent_links pl
            INNER JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = pl.parentuserid AND u.isactive = true
            GROUP BY pl.parentuserid, u.username, u.email
            ORDER BY ParentName;
            """;
        var rows = await connection.QueryAsync<NoticeFeeParentRow>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actor = ResolveUpdateActor();
        DateTime now = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableNotices}
            SET isactive = false, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Actor = actor, Now = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<NoticeListRow>> GetInactiveListAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT n.id AS Id, n.title AS Title, n.status AS Status, n.targettype AS TargetType,
                   n.contenttype AS ContentType, n.isactive AS IsActive,
                   n.requiresresponse AS RequiresResponse, n.responsedeadline AS ResponseDeadline,
                   n.publishedon AS PublishedOn,
                   (SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableNoticeResponses} r
                    WHERE r.noticeid = n.id AND r.isactive = true) AS ResponseCount
            FROM {Schema}.{DatabaseConfig.TableNotices} n
            WHERE n.isactive = false
            ORDER BY n.updatedon DESC;
            """;
        var rows = await connection.QueryAsync<NoticeListRow>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    private static object Map(NoticeEntity entity) => new
    {
        entity.Id,
        entity.Title,
        entity.Body,
        entity.CreatedByUserId,
        entity.PublishedOn,
        entity.RequiresResponse,
        entity.ResponseDeadline,
        TargetType = (short)entity.TargetType,
        entity.TargetRefId,
        ContentType = (short)entity.ContentType,
        entity.ContentJson,
        Status = (short)entity.Status,
        entity.IsActive,
        entity.VersionNo,
        entity.CreatedBy,
        entity.CreatedOn,
        entity.UpdatedBy,
        entity.UpdatedOn
    };
}
