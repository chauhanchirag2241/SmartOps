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

    private string G => IdentitySchema;

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
            SELECT DISTINCT t.userid AS Id,
                   TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) AS Name,
                   u.email AS Subtitle
            FROM {Schema}.{DatabaseConfig.TableEmployees} t
            INNER JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = t.userid
            WHERE t.isactive = true
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<NoticeAudienceRow>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public Task<IList<NoticeAudienceRow>> GetParentAudienceAsync(Guid? classId, CancellationToken ct = default)
    {
        // Parent portal accounts are no longer provisioned; there is no parent audience to target.
        return Task.FromResult<IList<NoticeAudienceRow>>(Array.Empty<NoticeAudienceRow>());
    }

    public async Task<IList<NoticeAudienceRow>> GetSchoolUserAudienceAsync(Guid schoolId, CancellationToken ct = default)
    {
        _ = schoolId;
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT u.id AS Id,
                   COALESCE(NULLIF(TRIM(u.username), ''), u.email) AS Name,
                   u.email AS Subtitle
            FROM {G}.{DatabaseConfig.TableUsers} u
            WHERE u.isactive = true
            ORDER BY Name;
            """;
        var rows = await connection.QueryAsync<NoticeAudienceRow>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public Task<IList<NoticeFeeParentRow>> GetPendingFeeParentTargetsAsync(Guid academicYearId, CancellationToken ct = default)
    {
        // Parent portal accounts are no longer provisioned; there is no parent fee-reminder audience to target.
        return Task.FromResult<IList<NoticeFeeParentRow>>(Array.Empty<NoticeFeeParentRow>());
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
