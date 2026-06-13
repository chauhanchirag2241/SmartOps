using Dapper;
using SmartOps.Application.Modules.Department;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Department;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly DapperContext _context;

    public DepartmentRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DepartmentEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
SELECT id AS Id, code AS Code, name AS Name, isactive AS IsActive
FROM {_context.OperationalSchema}.{DatabaseConfig.TableDepartments}
WHERE isactive = true
ORDER BY name ASC
""";

        var rows = await connection.QueryAsync<DepartmentEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }
}
