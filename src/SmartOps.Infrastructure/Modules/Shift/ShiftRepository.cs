using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Shift;
using SmartOps.Domain.Modules.Shift.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Shift;

public sealed class ShiftRepository : BaseRepository, IShiftRepository
{
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public ShiftRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    public async Task<Guid> CreateAsync(ShiftEntity shift, CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        if (shift.Id == Guid.Empty)
        {
            shift.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(shift, utcNow);
        shift.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(shift.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableShifts, shift, tx)
                .ConfigureAwait(false);
            return shift.Id;
        }).ConfigureAwait(false);
    }

    public async Task<PagedResult<ShiftListModel>> GetAllAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "s", ref whereClause);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableShifts;

        var countSql = $"SELECT COUNT(*) FROM {schema}.{table} s {whereClause};";
        var querySql = $"""
            SELECT
                s.id AS Id,
                s.shiftname AS ShiftName,
                s.starttime AS StartTime,
                s.endtime AS EndTime,
                s.displayorder AS DisplayOrder,
                s.isactive AS IsActive
            FROM {schema}.{table} s
            {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                countSql,
                new { SearchTerm = searchTerm, Filter = filter, ActiveBranchId = _branchContext.ActiveBranchId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var items = (await connection.QueryAsync<ShiftListModel>(
            new CommandDefinition(
                querySql,
                new
                {
                    SearchTerm = searchTerm,
                    Filter = filter,
                    ActiveBranchId = _branchContext.ActiveBranchId,
                    Offset = Math.Max(0, (pageIndex - 1) * pageSize),
                    PageSize = pageSize
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return new PagedResult<ShiftListModel>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<DropdownDto>> GetDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        string where = "WHERE s.isactive = true";
        where = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "s", ref where);

        var sql = $"""
            SELECT
                s.id AS Id,
                (s.shiftname || ' (' || s.starttime || ' - ' || s.endtime || ')') AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableShifts} s
            {where}
            ORDER BY s.displayorder ASC, s.shiftname ASC;
            """;

        var rows = await connection.QueryAsync<DropdownDto>(
            new CommandDefinition(
                sql,
                new { ActiveBranchId = _branchContext.ActiveBranchId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ShiftEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableShifts}
            WHERE id = @Id{activeFilter};
            """;
        return await connection.QuerySingleOrDefaultAsync<ShiftEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(ShiftEntity shift, CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(shift, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableShifts, shift, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableShifts, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active")
            {
                where += " AND s.isactive = true";
            }
            else if (filter == "Inactive")
            {
                where += " AND s.isactive = false";
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND s.shiftname ILIKE @SearchTerm";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "s.displayorder ASC, s.shiftname ASC, s.id ASC";
        }

        return sortColumn.ToLowerInvariant() switch
        {
            "shiftname" => $"s.shiftname {direction}, s.id ASC",
            "starttime" => $"s.starttime {direction}, s.id ASC",
            "endtime" => $"s.endtime {direction}, s.id ASC",
            "displayorder" => $"s.displayorder {direction}, s.id ASC",
            _ => "s.displayorder ASC, s.shiftname ASC, s.id ASC"
        };
    }
}
