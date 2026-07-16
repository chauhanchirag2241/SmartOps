using Dapper;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Department;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Department;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly DapperContext _context;
    private readonly IBranchContext _branchContext;

    public DepartmentRepository(DapperContext context, IBranchContext branchContext)
    {
        _context = context;
        _branchContext = branchContext;
    }

    public async Task<IReadOnlyList<DepartmentEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "d", cancellationToken)
            .ConfigureAwait(false);

        var sql = $"""
SELECT d.id AS Id, d.code AS Code, d.name AS Name, d.isactive AS IsActive
FROM {_context.OperationalSchema}.{DatabaseConfig.TableDepartments} d
WHERE d.isactive = true{branchFilter}
ORDER BY d.name ASC
""";

        var rows = await connection.QueryAsync<DepartmentEntity>(
            new CommandDefinition(sql, new { ActiveBranchId = activeBranchId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }
}
