using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Timetable;
using SmartOps.Domain.Modules.Timetable.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Timetable;

public sealed class PeriodTemplateRepository : BaseRepository, IPeriodTemplateRepository
{
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public PeriodTemplateRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    private string Schema => Context.OperationalSchema;

    public async Task<Guid> CreateTemplateAsync(
        PeriodTemplateEntity template,
        IReadOnlyList<PeriodEntity> periods,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        if (template.Id == Guid.Empty) template.Id = Guid.NewGuid();
        EnsureInsertAudit(template, utcNow);
        template.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(template.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Schema, DatabaseConfig.TablePeriodTemplates, template, tx).ConfigureAwait(false);
            foreach (var period in periods)
            {
                if (period.Id == Guid.Empty) period.Id = Guid.NewGuid();
                period.TemplateId = template.Id;
                EnsureInsertAudit(period, utcNow);
                await InsertAsync(conn, Schema, DatabaseConfig.TablePeriods, period, tx).ConfigureAwait(false);
            }

            return template.Id;
        }).ConfigureAwait(false);
    }

    public async Task UpdateTemplateAsync(
        PeriodTemplateEntity template,
        IReadOnlyList<PeriodEntity> periods,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var actor = ResolveUpdateActor();
        ApplyUpdateAudit(template, actor, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Schema, DatabaseConfig.TablePeriodTemplates, template, tx, "Id")
                .ConfigureAwait(false);

            await SoftDeleteRelatedAsync(conn, Schema, DatabaseConfig.TablePeriods, "templateid", template.Id, tx)
                .ConfigureAwait(false);

            foreach (var period in periods)
            {
                if (period.Id == Guid.Empty) period.Id = Guid.NewGuid();
                period.TemplateId = template.Id;
                EnsureInsertAudit(period, utcNow);
                await InsertAsync(conn, Schema, DatabaseConfig.TablePeriods, period, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteRelatedAsync(conn, Schema, DatabaseConfig.TablePeriods, "templateid", id, tx)
                .ConfigureAwait(false);
            await SoftDeleteAsync(conn, Schema, DatabaseConfig.TablePeriodTemplates, id, tx).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<PagedResult<PeriodTemplateListModel>> GetAllTemplatesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "t", ref whereClause);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

        var countSql = $@"SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TablePeriodTemplates} t {whereClause};";
        var querySql = $@"
            SELECT
                t.id AS Id,
                t.name AS Name,
                t.description AS Description,
                COALESCE((
                    SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TablePeriods} p
                    WHERE p.templateid = t.id AND p.isactive = true
                ), 0) AS PeriodCount,
                COALESCE((
                    SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TablePeriods} p
                    WHERE p.templateid = t.id AND p.isactive = true AND p.isbreak = false
                ), 0) AS TeachingPeriodCount,
                t.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TablePeriodTemplates} t
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<PeriodTemplateListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm, ActiveBranchId = _branchContext.ActiveBranchId },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DropdownDto>> GetTemplateDropdownAsync(CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        string whereClause = "WHERE t.isactive = true";
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "t", ref whereClause);

        var sql = $@"
            SELECT t.id AS Id, t.name AS Name
            FROM {Schema}.{DatabaseConfig.TablePeriodTemplates} t
            {whereClause}
            ORDER BY t.name ASC;";

        var items = await connection.QueryAsync<DropdownDto>(
            sql, new { ActiveBranchId = _branchContext.ActiveBranchId }).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<PeriodTemplateEntity?> GetTemplateByIdAsync(
        Guid id,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $@"
            SELECT * FROM {Schema}.{DatabaseConfig.TablePeriodTemplates}
            WHERE id = @Id{activeFilter};";
        return await connection.QuerySingleOrDefaultAsync<PeriodTemplateEntity>(sql, new { Id = id })
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PeriodEntity>> GetPeriodsByTemplateIdAsync(
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
            SELECT
                id AS Id,
                templateid AS TemplateId,
                name AS Name,
                shortname AS ShortName,
                periodorder AS PeriodOrder,
                starttime AS StartTime,
                endtime AS EndTime,
                isbreak AS IsBreak,
                isactive AS IsActive,
                versionno AS VersionNo,
                createdby AS CreatedBy,
                createdon AS CreatedOn,
                updatedby AS UpdatedBy,
                updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePeriods}
            WHERE templateid = @TemplateId AND isactive = true
            ORDER BY periodorder ASC;";

        var rows = await connection.QueryAsync<PeriodEntity>(sql, new { TemplateId = templateId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active") where += " AND t.isactive = true";
            else if (filter == "Inactive") where += " AND t.isactive = false";
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (t.name ILIKE @SearchTerm OR COALESCE(t.description, '') ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn)) return "t.name ASC, t.id ASC";
        return sortColumn.ToLowerInvariant() switch
        {
            "name" => $"t.name {direction}",
            "periodcount" => $"PeriodCount {direction}",
            "teachingperiodcount" => $"TeachingPeriodCount {direction}",
            _ => "t.name ASC, t.id ASC",
        };
    }
}
