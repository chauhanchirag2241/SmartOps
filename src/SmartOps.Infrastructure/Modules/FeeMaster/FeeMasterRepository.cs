using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FeeMaster;

public sealed class FeeMasterRepository : BaseRepository, IFeeMasterRepository
{
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public FeeMasterRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    public async Task<Guid> CreateAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        if (fee.Id == Guid.Empty)
        {
            fee.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(fee, utcNow);
        fee.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(fee.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeMaster, fee, tx)
                .ConfigureAwait(false);
            return fee.Id;
        }).ConfigureAwait(false);
    }

    public async Task<PagedResult<FeeMasterListModel>> GetAllAsync(
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
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "f", ref whereClause);
        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableFeeMaster;

        var countSql = $"SELECT COUNT(*) FROM {schema}.{table} f {whereClause};";
        var querySql = $"""
            SELECT
                f.id AS Id,
                f.feename AS FeeName,
                f.feetype AS FeeType,
                f.publishedon AS PublishedOn,
                f.defaultduedate AS DefaultDueDate,
                f.applicableto AS ApplicableTo,
                f.description AS Description,
                f.isactive AS IsActive
            FROM {schema}.{table} f
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

        var items = (await connection.QueryAsync<FeeMasterListModel>(
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

        return new PagedResult<FeeMasterListModel>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<FeeMasterEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableFeeMaster}
            WHERE id = @Id{activeFilter};
            """;
        return await connection.QuerySingleOrDefaultAsync<FeeMasterEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(fee, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeMaster, fee, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task UpdateBasicAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(fee, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableFeeMaster;
        var sql = $"""
            UPDATE {schema}.{table}
            SET feename = @FeeName,
                publishedon = @PublishedOn,
                defaultduedate = @DefaultDueDate,
                description = @Description,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        fee.Id,
                        fee.FeeName,
                        fee.PublishedOn,
                        fee.DefaultDueDate,
                        fee.Description,
                        fee.UpdatedBy,
                        fee.UpdatedOn,
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableFeeMaster, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetClassGroupIdsAsync(
        Guid feeMasterId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
            SELECT classgroupid
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableFeeMasterClassGroup}
            WHERE feemasterid = @FeeMasterId AND isactive = true
            ORDER BY createdon ASC;
            """;
        var ids = (await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { FeeMasterId = feeMasterId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();
        return ids;
    }

    public async Task SaveClassGroupIdsAsync(
        Guid feeMasterId,
        Guid branchId,
        IReadOnlyList<Guid> classGroupIds,
        bool allowRemove,
        CancellationToken cancellationToken = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var desired = (classGroupIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableFeeMasterClassGroup;
        var resolvedBranch = await _branchWrite
            .ResolveWriteBranchIdAsync(branchId, cancellationToken)
            .ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var existing = (await conn.QueryAsync<(Guid Id, Guid ClassGroupId)>(
                new CommandDefinition(
                    $"""
                    SELECT id AS Id, classgroupid AS ClassGroupId
                    FROM {schema}.{table}
                    WHERE feemasterid = @FeeMasterId AND isactive = true;
                    """,
                    new { FeeMasterId = feeMasterId },
                    transaction: tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false)).ToList();

            var existingByGroup = existing.ToDictionary(x => x.ClassGroupId, x => x.Id);
            var toAdd = desired.Where(id => !existingByGroup.ContainsKey(id)).ToList();

            if (allowRemove)
            {
                var toRemove = existing.Where(x => !desired.Contains(x.ClassGroupId)).Select(x => x.Id).ToList();
                foreach (var rowId in toRemove)
                {
                    await SoftDeleteAsync(conn, schema, table, rowId, tx).ConfigureAwait(false);
                }
            }

            foreach (var classGroupId in toAdd)
            {
                var row = new FeeMasterClassGroupEntity
                {
                    Id = Guid.NewGuid(),
                    BranchId = resolvedBranch,
                    FeeMasterId = feeMasterId,
                    ClassGroupId = classGroupId,
                };
                EnsureInsertAudit(row, utcNow);
                await InsertAsync(conn, schema, table, row, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";
        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active")
            {
                where += " AND f.isactive = true";
            }
            else if (filter == "Inactive")
            {
                where += " AND f.isactive = false";
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND f.feename ILIKE @SearchTerm";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "f.feename ASC, f.id ASC";
        }

        return sortColumn.ToLowerInvariant() switch
        {
            "feename" => $"f.feename {direction}, f.id ASC",
            "feetype" or "feetypelabel" => $"f.feetype {direction}, f.id ASC",
            "publishedon" => $"f.publishedon {direction} NULLS LAST, f.id ASC",
            "defaultduedate" => $"f.defaultduedate {direction} NULLS LAST, f.id ASC",
            "applicableto" or "applicabletolabel" => $"f.applicableto {direction}, f.id ASC",
            "isactive" => $"f.isactive {direction}, f.id ASC",
            _ => "f.feename ASC, f.id ASC"
        };
    }
}
