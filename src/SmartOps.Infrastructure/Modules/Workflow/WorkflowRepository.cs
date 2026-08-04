using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Notice;
using SmartOps.Domain.Modules.Workflow;
using SmartOps.Domain.Modules.Workflow.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Workflow;

public sealed class WorkflowRepository : BaseRepository, IWorkflowRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public WorkflowRepository(
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

    public async Task<Guid> CreateItemAsync(WorkflowItemEntity item, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
        EnsureInsertAudit(item, now, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableWorkflowItems}
                (id, assigneeuserid, itemtype, status, referencetype, referenceid, title, summary,
                 duedate, priority, payloadjson, completedbyuserid, completedon, outcome,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @AssigneeUserId, @ItemType, @Status, @ReferenceType, @ReferenceId, @Title, @Summary,
                 @DueDate, @Priority, @PayloadJson, @CompletedByUserId, @CompletedOn, @Outcome,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, MapItem(item), cancellationToken: ct))
            .ConfigureAwait(false);
        return item.Id;
    }

    public async Task UpdateItemAsync(WorkflowItemEntity item, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(item, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableWorkflowItems}
            SET status = @Status, completedbyuserid = @CompletedByUserId, completedon = @CompletedOn,
                outcome = @Outcome, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, MapItem(item), cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<WorkflowItemEntity?> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, assigneeuserid AS AssigneeUserId, itemtype AS ItemType, status AS Status,
                   referencetype AS ReferenceType, referenceid AS ReferenceId, title AS Title, summary AS Summary,
                   duedate AS DueDate, priority AS Priority, payloadjson AS PayloadJson,
                   completedbyuserid AS CompletedByUserId, completedon AS CompletedOn, outcome AS Outcome,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableWorkflowItems}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<WorkflowItemEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<WorkflowItemEntity>> GetPendingForUserAsync(
        Guid userId,
        short? itemType,
        string? search,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder($"""
            SELECT w.id AS Id, w.assigneeuserid AS AssigneeUserId, w.itemtype AS ItemType, w.status AS Status,
                   w.referencetype AS ReferenceType, w.referenceid AS ReferenceId, w.title AS Title, w.summary AS Summary,
                   w.duedate AS DueDate, w.priority AS Priority, w.payloadjson AS PayloadJson,
                   w.completedbyuserid AS CompletedByUserId, w.completedon AS CompletedOn, w.outcome AS Outcome,
                   w.isactive AS IsActive, w.versionno AS VersionNo, w.createdby AS CreatedBy, w.createdon AS CreatedOn,
                   w.updatedby AS UpdatedBy, w.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableWorkflowItems} w
            WHERE w.isactive = true AND w.assigneeuserid = @UserId AND w.status = @Pending
              AND (
                w.referencetype <> @NoticeRef
                OR EXISTS (
                    SELECT 1
                    FROM {Schema}.{DatabaseConfig.TableNotices} n
                    WHERE n.id = w.referenceid
                      AND n.isactive = true
                      AND n.status = @PublishedStatus
                      AND n.publishedon IS NOT NULL
                      AND n.publishedon <= @NowUtc
                )
              )
            """);

        if (itemType.HasValue)
        {
            sb.Append(" AND w.itemtype = @ItemType");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sb.Append(" AND (w.title ILIKE @Search OR w.summary ILIKE @Search)");
        }

        sb.Append(" ORDER BY w.priority DESC, w.duedate NULLS LAST, w.createdon DESC");

        var rows = await connection.QueryAsync<WorkflowItemEntity>(new CommandDefinition(
            sb.ToString(),
            new
            {
                UserId = userId,
                Pending = (short)WorkflowItemStatus.Pending,
                ItemType = itemType,
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                NoticeRef = (short)WorkflowReferenceType.Notice,
                PublishedStatus = (short)NoticeStatus.Published,
                NowUtc = DateTimeOffset.UtcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task<MyActionStatsRow> GetStatsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT
                COUNT(*) FILTER (WHERE visible) AS TotalPending,
                COUNT(*) FILTER (WHERE visible AND itemtype = @LeaveApproval) AS LeaveApprovals,
                COUNT(*) FILTER (WHERE visible AND itemtype = @NoticeResponse) AS NoticeResponses,
                COUNT(*) FILTER (WHERE visible AND itemtype = @FormFill) AS FormFills
            FROM (
                SELECT
                    w.itemtype,
                    w.status = @Pending
                    AND (
                        w.referencetype <> @NoticeRef
                        OR EXISTS (
                            SELECT 1
                            FROM {Schema}.{DatabaseConfig.TableNotices} n
                            WHERE n.id = w.referenceid
                              AND n.isactive = true
                              AND n.status = @PublishedStatus
                              AND n.publishedon IS NOT NULL
                              AND n.publishedon <= @NowUtc
                        )
                    ) AS visible
                FROM {Schema}.{DatabaseConfig.TableWorkflowItems} w
                WHERE w.isactive = true AND w.assigneeuserid = @UserId AND w.status = @Pending
            ) x;
            """;

        return await connection.QuerySingleAsync<MyActionStatsRow>(new CommandDefinition(sql, new
        {
            UserId = userId,
            Pending = (short)WorkflowItemStatus.Pending,
            LeaveApproval = (short)WorkflowItemType.LeaveApproval,
            NoticeResponse = (short)WorkflowItemType.NoticeResponse,
            FormFill = (short)WorkflowItemType.FormFill,
            NoticeRef = (short)WorkflowReferenceType.Notice,
            PublishedStatus = (short)NoticeStatus.Published,
            NowUtc = DateTimeOffset.UtcNow
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task CancelPendingForReferenceAsync(WorkflowReferenceType refType, Guid refId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableWorkflowItems}
            SET status = @Cancelled, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE isactive = true AND referencetype = @RefType AND referenceid = @RefId AND status = @Pending;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Cancelled = (short)WorkflowItemStatus.Cancelled,
            ActorId = actorId,
            Now = SchoolLocalTime.NowDateTime(),
            RefType = (short)refType,
            RefId = refId,
            Pending = (short)WorkflowItemStatus.Pending
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> InsertActionAsync(
        Guid workflowItemId,
        string actionCode,
        string? comment,
        Guid actorUserId,
        string? metadataJson,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid id = Guid.NewGuid();
        Guid actor = ResolveInsertActor();
        DateTime now = SchoolLocalTime.NowDateTime();

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableWorkflowItemActions}
                (id, workflowitemid, actioncode, comment, actoruserid, actedon, metadatajson,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @WorkflowItemId, @ActionCode, @Comment, @ActorUserId, @ActedOn, @MetadataJson,
                 true, 1, @Actor, @Now, @Actor, @Now);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            WorkflowItemId = workflowItemId,
            ActionCode = actionCode,
            Comment = comment,
            ActorUserId = actorUserId,
            ActedOn = SchoolLocalTime.NowDateTime(),
            MetadataJson = metadataJson,
            Actor = actor,
            Now = now
        }, cancellationToken: ct)).ConfigureAwait(false);

        return id;
    }

    public async Task<int> CountPendingForReferenceAsync(
        WorkflowReferenceType refType,
        Guid refId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(1)
            FROM {Schema}.{DatabaseConfig.TableWorkflowItems}
            WHERE isactive = true AND referencetype = @RefType AND referenceid = @RefId AND status = @Pending;
            """;

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            RefType = (short)refType,
            RefId = refId,
            Pending = (short)WorkflowItemStatus.Pending
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<WorkflowItemEntity?> GetPendingByReferenceForUserAsync(
        WorkflowReferenceType refType,
        Guid refId,
        Guid userId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, assigneeuserid AS AssigneeUserId, itemtype AS ItemType, status AS Status,
                   referencetype AS ReferenceType, referenceid AS ReferenceId, title AS Title, summary AS Summary,
                   duedate AS DueDate, priority AS Priority, payloadjson AS PayloadJson,
                   completedbyuserid AS CompletedByUserId, completedon AS CompletedOn, outcome AS Outcome,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableWorkflowItems}
            WHERE isactive = true AND referencetype = @RefType AND referenceid = @RefId
              AND assigneeuserid = @UserId AND status = @Pending
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<WorkflowItemEntity>(new CommandDefinition(sql, new
        {
            RefType = (short)refType,
            RefId = refId,
            UserId = userId,
            Pending = (short)WorkflowItemStatus.Pending
        }, cancellationToken: ct)).ConfigureAwait(false);
    }

    private static object MapItem(WorkflowItemEntity item) => new
    {
        item.Id,
        item.AssigneeUserId,
        ItemType = (short)item.ItemType,
        Status = (short)item.Status,
        ReferenceType = (short)item.ReferenceType,
        item.ReferenceId,
        item.Title,
        item.Summary,
        item.DueDate,
        item.Priority,
        item.PayloadJson,
        item.CompletedByUserId,
        item.CompletedOn,
        item.Outcome,
        item.IsActive,
        item.VersionNo,
        item.CreatedBy,
        item.CreatedOn,
        item.UpdatedBy,
        item.UpdatedOn
    };
}
