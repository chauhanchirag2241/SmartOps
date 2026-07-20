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

public sealed class PeriodRepository : BaseRepository, IPeriodRepository
{
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public PeriodRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    public async Task<Guid> CreatePeriodAsync(PeriodEntity period, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        if (period.Id == Guid.Empty)
        {
            period.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(period, utcNow);
        period.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(period.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TablePeriods, period, tx)
                .ConfigureAwait(false);
            return period.Id;
        }).ConfigureAwait(false);
    }

    public async Task<PagedResult<PeriodListModel>> GetAllPeriodsAsync(
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
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "p", ref whereClause);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TablePeriods;

        var countSql = $@"SELECT COUNT(*) FROM {schema}.{table} p {whereClause};";
        var querySql = $@"
            SELECT
                p.id AS Id,
                p.name AS Name,
                p.shortname AS ShortName,
                p.periodorder AS PeriodOrder,
                p.starttime AS StartTime,
                p.endtime AS EndTime,
                (p.starttime || ' – ' || p.endtime) AS TimeLabel,
                p.isbreak AS IsBreak,
                p.isactive AS IsActive
            FROM {schema}.{table} p
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<PeriodListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm, ActiveBranchId = _branchContext.ActiveBranchId },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DropdownDto>> GetPeriodDropdownAsync(CancellationToken cancellationToken)
    {
        var periods = await GetActivePeriodsOrderedAsync(cancellationToken).ConfigureAwait(false);
        return periods
            .Select(p => new DropdownDto { Id = p.Id, Name = $"{p.PeriodOrder}. {p.Name}" })
            .ToList();
    }

    public async Task<IReadOnlyList<PeriodEntity>> GetActivePeriodsOrderedAsync(CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        string whereClause = "WHERE p.isactive = true";
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "p", ref whereClause);

        var sql = $@"
            SELECT
                p.id AS Id,
                p.branchid AS BranchId,
                p.name AS Name,
                p.shortname AS ShortName,
                p.periodorder AS PeriodOrder,
                p.starttime AS StartTime,
                p.endtime AS EndTime,
                p.isbreak AS IsBreak,
                p.isactive AS IsActive,
                p.versionno AS VersionNo,
                p.createdby AS CreatedBy,
                p.createdon AS CreatedOn,
                p.updatedby AS UpdatedBy,
                p.updatedon AS UpdatedOn
            FROM {Context.OperationalSchema}.{DatabaseConfig.TablePeriods} p
            {whereClause}
            ORDER BY p.periodorder ASC;";

        var items = await connection.QueryAsync<PeriodEntity>(
            sql,
            new { ActiveBranchId = _branchContext.ActiveBranchId }).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<PeriodEntity?> GetPeriodByIdAsync(Guid id, CancellationToken cancellationToken, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TablePeriods}
            WHERE id = @Id{activeFilter};";
        return await connection.QuerySingleOrDefaultAsync<PeriodEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task UpdatePeriodAsync(PeriodEntity period, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        ApplyUpdateAudit(period, ResolveUpdateActor(), utcNow);
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TablePeriods, period, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TablePeriods, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active") where += " AND p.isactive = true";
            else if (filter == "Inactive") where += " AND p.isactive = false";
            else if (filter == "Break") where += " AND p.isbreak = true";
            else if (filter == "Teaching") where += " AND p.isbreak = false";
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (p.name ILIKE @SearchTerm OR p.shortname ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "p.periodorder ASC, p.id ASC";
        }

        return sortColumn.ToLowerInvariant() switch
        {
            "name" => $"p.name {direction}",
            "shortname" => $"p.shortname {direction}",
            "periodorder" => $"p.periodorder {direction}",
            "starttime" => $"p.starttime {direction}",
            "endtime" => $"p.endtime {direction}",
            _ => "p.periodorder ASC, p.id ASC",
        };
    }
}
