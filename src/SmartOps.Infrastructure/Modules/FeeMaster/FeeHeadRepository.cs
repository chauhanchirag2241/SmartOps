using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FeeMaster;

public sealed class FeeHeadRepository : BaseRepository, IFeeHeadRepository
{
    private readonly IBranchScopedWriteHelper _branchWrite;

    public FeeHeadRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchWrite = branchWrite;
    }

    public async Task<PagedResult<FeeHeadListModel>> GetByFeeMasterAsync(
        Guid feeMasterId,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableFeeHead;

        var countSql = $"SELECT COUNT(*) FROM {schema}.{table} h {whereClause};";
        var querySql = $"""
            SELECT
                h.id AS Id,
                h.feeheadname AS FeeHeadName,
                h.ismandatory AS IsMandatory,
                h.iseditable AS IsEditable,
                h.amount AS Amount,
                h.applicablemonths AS ApplicableMonths,
                h.isactive AS IsActive
            FROM {schema}.{table} h
            {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var param = new
        {
            FeeMasterId = feeMasterId,
            SearchTerm = searchTerm,
            Offset = Math.Max(0, (pageIndex - 1) * pageSize),
            PageSize = pageSize,
        };

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, param, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var items = (await connection.QueryAsync<FeeHeadListModel>(
            new CommandDefinition(querySql, param, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return new PagedResult<FeeHeadListModel>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task<FeeHeadDetailModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var activeFilter = includeInactive ? string.Empty : " AND h.isactive = true";

        var headSql = $"""
            SELECT
                h.id AS Id,
                h.feemasterid AS FeeMasterId,
                h.feeheadname AS FeeHeadName,
                h.ismandatory AS IsMandatory,
                h.iseditable AS IsEditable,
                h.amount AS Amount,
                h.applicablemonths AS ApplicableMonths,
                h.isactive AS IsActive
            FROM {schema}.{DatabaseConfig.TableFeeHead} h
            WHERE h.id = @Id{activeFilter};
            """;

        var head = await connection.QuerySingleOrDefaultAsync<FeeHeadDetailModel>(
            new CommandDefinition(headSql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (head is null)
        {
            return null;
        }

        var periodsSql = $"""
            SELECT
                p.id AS Id,
                p.classgroupid AS ClassGroupId,
                cg.classname AS ClassGroupName,
                p.academicperiodid AS AcademicPeriodId,
                ap.name AS AcademicPeriodName,
                p.amount AS Amount
            FROM {schema}.{DatabaseConfig.TableFeeHeadPeriodAmount} p
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = p.classgroupid
            INNER JOIN {schema}.{DatabaseConfig.TableClassAcademicPeriods} ap ON ap.id = p.academicperiodid
            WHERE p.feeheadid = @Id AND p.isactive = true
            ORDER BY cg.classname ASC, ap.periodindex ASC;
            """;

        var periods = (await connection.QueryAsync<FeeHeadPeriodAmountModel>(
            new CommandDefinition(periodsSql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        head.PeriodAmounts = periods;
        return head;
    }

    public async Task<FeeHeadEntity?> GetEntityByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableFeeHead}
            WHERE id = @Id{activeFilter};
            """;
        return await connection.QuerySingleOrDefaultAsync<FeeHeadEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateAsync(
        FeeHeadEntity head,
        IReadOnlyList<FeeHeadPeriodAmountEntity> periodAmounts,
        CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        if (head.Id == Guid.Empty)
        {
            head.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(head, now);
        head.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(head.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeHead, head, tx)
                .ConfigureAwait(false);

            foreach (var period in periodAmounts)
            {
                if (period.Id == Guid.Empty)
                {
                    period.Id = Guid.NewGuid();
                }

                period.FeeHeadId = head.Id;
                EnsureInsertAudit(period, now);
                await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeHeadPeriodAmount, period, tx)
                    .ConfigureAwait(false);
            }

            return head.Id;
        }).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        FeeHeadEntity head,
        IReadOnlyList<FeeHeadPeriodAmountEntity> periodAmounts,
        CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(head, actorId, now);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeHead, head, tx, "Id")
                .ConfigureAwait(false);

            await SoftDeleteRelatedAsync(
                    conn,
                    Context.OperationalSchema,
                    DatabaseConfig.TableFeeHeadPeriodAmount,
                    "feeheadid",
                    head.Id,
                    tx)
                .ConfigureAwait(false);

            foreach (var period in periodAmounts)
            {
                if (period.Id == Guid.Empty)
                {
                    period.Id = Guid.NewGuid();
                }

                period.FeeHeadId = head.Id;
                EnsureInsertAudit(period, now);
                await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeHeadPeriodAmount, period, tx)
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteRelatedAsync(
                    conn,
                    Context.OperationalSchema,
                    DatabaseConfig.TableFeeHeadPeriodAmount,
                    "feeheadid",
                    id,
                    tx)
                .ConfigureAwait(false);
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeHead, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE h.feemasterid = @FeeMasterId";
        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active")
            {
                where += " AND h.isactive = true";
            }
            else if (filter == "Inactive")
            {
                where += " AND h.isactive = false";
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND h.feeheadname ILIKE @SearchTerm";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "h.feeheadname ASC, h.id ASC";
        }

        return sortColumn.ToLowerInvariant() switch
        {
            "feeheadname" => $"h.feeheadname {direction}, h.id ASC",
            "ismandatory" => $"h.ismandatory {direction}, h.id ASC",
            "iseditable" => $"h.iseditable {direction}, h.id ASC",
            "amount" => $"h.amount {direction} NULLS LAST, h.id ASC",
            "isactive" => $"h.isactive {direction}, h.id ASC",
            _ => "h.feeheadname ASC, h.id ASC"
        };
    }
}
