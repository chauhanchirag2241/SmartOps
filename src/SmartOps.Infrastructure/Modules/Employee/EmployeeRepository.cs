using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Employee;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Employee;

public sealed class EmployeeRepository : BaseRepository, IEmployeeRepository
{
    private readonly IUserScopeContext _scope;

    public EmployeeRepository(DapperContext context, ICurrentUserService currentUser, IUserScopeContext scope)
        : base(context, currentUser)
    {
        _scope = scope;
    }

    public async Task<Guid> CreateEmployeeAsync(EmployeeEntity employee, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (employee.Id == Guid.Empty)
        {
            employee.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(employee, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var employeeId = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableEmployees, employee, tx)
                .ConfigureAwait(false);
            return employeeId;
        }).ConfigureAwait(false);
    }

    public async Task<EmployeeEntity?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} WHERE id = @Id{activeFilter}";
        return await connection.QuerySingleOrDefaultAsync<EmployeeEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task<PagedResult<EmployeeListModel>> GetAllEmployeesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StaffFilter filter = StaffFilter.All,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = "WHERE 1 = 1";

        switch (filter)
        {
            case StaffFilter.Active:
                whereClause += " AND e.isactive = true";
                break;
            case StaffFilter.Inactive:
                whereClause += " AND e.isactive = false";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereClause += " AND (e.firstname ILIKE @SearchTerm OR e.lastname ILIKE @SearchTerm OR e.employeeid ILIKE @SearchTerm OR e.email ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedEmployeeIds.Count > 0)
            {
                whereClause += " AND e.id = ANY(@ScopeEmployeeIds)";
            }
            else if (_scope.AllowedDepartmentIds.Count > 0)
            {
                whereClause += " AND e.departmentid = ANY(@ScopeDepartmentIds)";
            }
            else
            {
                whereClause += " AND 1 = 0";
            }
        }

        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderBy = string.IsNullOrWhiteSpace(sortColumn) ? "e.createdon DESC" : $"e.{sortColumn} {direction}";

        var countSql = $"""
SELECT COUNT(*)
FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
{whereClause}
""";

        var querySql = $@"
            SELECT
                e.id,
                TRIM(e.firstname || ' ' || e.lastname) AS Name,
                e.email,
                e.designation,
                e.usertypecode AS UserTypeCode,
                d.name AS DepartmentName,
                TRIM(rm.firstname || ' ' || rm.lastname) AS ReportingManagerName,
                e.isactive
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableDepartments} d ON d.id = e.departmentid
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} rm ON rm.id = e.reportingmanagerid
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<EmployeeListModel>(
            connection,
            querySql,
            countSql,
            new
            {
                SearchTerm = searchTerm,
                ScopeEmployeeIds = _scope.AllowedEmployeeIds.ToArray(),
                ScopeDepartmentIds = _scope.AllowedDepartmentIds.ToArray()
            },
            pageIndex,
            pageSize).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DropdownDto>> GetClassTeacherDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT
                id AS Id,
                TRIM(firstname || ' ' || lastname) AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees}
            WHERE isactive = true AND usertypecode = 'TEACHER'
            ORDER BY firstname ASC, lastname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<IReadOnlyList<DropdownDto>> GetReportingManagerDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT
                id AS Id,
                TRIM(firstname || ' ' || lastname) AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees}
            WHERE isactive = true
            ORDER BY firstname ASC, lastname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task UpdateEmployeeAsync(EmployeeEntity employee, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(employee, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableEmployees, employee, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task SetEmployeeUserIdAsync(Guid employeeId, Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableEmployees}
SET userid = @UserId, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
WHERE id = @EmployeeId AND isactive = true
""";
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    EmployeeId = employeeId,
                    UserId = userId,
                    Now = DateTime.UtcNow,
                    Actor = ResolveUpdateActor()
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableEmployees, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
