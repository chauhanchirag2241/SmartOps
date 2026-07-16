using System.Text;
using Dapper;
using SmartOps.Application.Modules.Branch;

namespace SmartOps.Infrastructure.Modules.Authorization.Sql;

public static class BranchSqlBuilder
{
    public static string AppendActiveBranchFilter(
        IBranchContext branchContext,
        string tableAlias,
        ref string whereClause)
    {
        if (!branchContext.IsResolved || branchContext.ActiveBranchId is null)
        {
            return whereClause;
        }

        whereClause = $"{whereClause} AND {tableAlias}.branchid = @ActiveBranchId";
        return whereClause;
    }

    public static string AppendSelectedBranchesFilter(
        IBranchContext branchContext,
        string tableAlias,
        ref string whereClause)
    {
        if (!branchContext.IsResolved || branchContext.SelectedBranchIds.Count == 0)
        {
            return AppendActiveBranchFilter(branchContext, tableAlias, ref whereClause);
        }

        whereClause = $"{whereClause} AND {tableAlias}.branchid = ANY(@SelectedBranchIds)";
        return whereClause;
    }

    public static async Task AppendActiveBranchFilterAsync(
        IBranchContext branchContext,
        StringBuilder sql,
        DynamicParameters parameters,
        string tableAlias,
        CancellationToken cancellationToken = default)
    {
        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        if (!branchContext.IsResolved || branchContext.ActiveBranchId is null)
        {
            return;
        }

        sql.Append($" AND {tableAlias}.branchid = @ActiveBranchId");
        parameters.Add("ActiveBranchId", branchContext.ActiveBranchId.Value);
    }

    public static async Task<(string SqlSuffix, Guid? ActiveBranchId)> GetActiveBranchFilterAsync(
        IBranchContext branchContext,
        string tableAlias,
        CancellationToken cancellationToken = default)
    {
        await branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        if (!branchContext.IsResolved || branchContext.ActiveBranchId is null)
        {
            return (string.Empty, null);
        }

        return ($" AND {tableAlias}.branchid = @ActiveBranchId", branchContext.ActiveBranchId);
    }
}
