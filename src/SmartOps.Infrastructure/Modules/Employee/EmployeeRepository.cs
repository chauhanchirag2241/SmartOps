using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Employee;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;

namespace SmartOps.Infrastructure.Modules.Employee;

public sealed class EmployeeRepository : BaseRepository, IEmployeeRepository
{
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;
    private readonly IUserProvisioningService _userProvisioning;

    public EmployeeRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite,
        IUserProvisioningService userProvisioning)
        : base(context, currentUser)
    {
        _scope = scope;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
        _userProvisioning = userProvisioning;
    }

    public async Task<Guid> CreateEmployeeAsync(
        EmployeeEntity employee,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        if (employee.Id == Guid.Empty)
        {
            employee.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(employee, now);

        employee.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(employee.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            Guid provisionedUserId = await _userProvisioning
                .ProvisionEmployeeUserAsync(employee, schoolId, tx, cancellationToken)
                .ConfigureAwait(false);
            employee.UserId = provisionedUserId;

            return await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableEmployees, employee, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<EmployeeEntity?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
SELECT
    e.*,
    u.firstname AS FirstName,
    u.lastname AS LastName,
    u.email AS Email,
    u.mobile AS Mobile,
    u.username AS Username,
    u.usertypeid AS UserTypeId
FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
WHERE e.id = @Id{activeFilter}
""";
        var row = await connection.QuerySingleOrDefaultAsync<EmployeeDetailRow>(sql, new { Id = id }).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        row.UserTypeCode = UserTypeCodes.GetName(row.UserTypeId) ?? row.UserTypeCode;
        return row;
    }

    public async Task<PagedResult<EmployeeListModel>> GetAllEmployeesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StaffFilter filter = StaffFilter.All,
        bool teachersOnly = false,
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

        if (teachersOnly)
        {
            whereClause += $" AND u.usertypeid = '{UserTypeCodes.Ids.Teacher}'";
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereClause += " AND (u.firstname ILIKE @SearchTerm OR u.lastname ILIKE @SearchTerm OR e.employeecode ILIKE @SearchTerm OR u.email ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "e", ref whereClause);

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
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
{whereClause}
""";

        var querySql = $@"
            SELECT
                e.id,
                TRIM(u.firstname || ' ' || u.lastname) AS Name,
                u.email,
                e.designation,
                u.usertypeid AS UserTypeId,
                d.name AS DepartmentName,
                TRIM(rmu.firstname || ' ' || rmu.lastname) AS ReportingManagerName,
                e.isactive
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableDepartments} d ON d.id = e.departmentid
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} rm ON rm.id = e.reportingmanagerid
            LEFT JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} rmu ON rmu.id = rm.userid
            {whereClause}
            ORDER BY {orderBy}";

        var page = await GetPagedResultAsync<EmployeeListRow>(
            connection,
            querySql,
            countSql,
            new
            {
                SearchTerm = searchTerm,
                ScopeEmployeeIds = _scope.AllowedEmployeeIds.ToArray(),
                ScopeDepartmentIds = _scope.AllowedDepartmentIds.ToArray(),
                ActiveBranchId = _branchContext.ActiveBranchId
            },
            pageIndex,
            pageSize).ConfigureAwait(false);

        return new PagedResult<EmployeeListModel>
        {
            Items = page.Items.Select(r => new EmployeeListModel
            {
                Id = r.Id,
                Name = r.Name,
                Email = r.Email,
                Designation = r.Designation,
                UserTypeCode = UserTypeCodes.GetName(r.UserTypeId) ?? string.Empty,
                DepartmentName = r.DepartmentName,
                ReportingManagerName = r.ReportingManagerName,
                IsActive = r.IsActive,
            }).ToList(),
            TotalCount = page.TotalCount,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize,
        };
    }

    public async Task<IReadOnlyList<DropdownDto>> GetClassTeacherDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "e", cancellationToken)
            .ConfigureAwait(false);

        var sql = $@"
            SELECT
                e.id AS Id,
                TRIM(u.firstname || ' ' || u.lastname) AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
            WHERE e.isactive = true AND u.usertypeid = '{UserTypeCodes.Ids.Teacher}'{branchFilter}
            ORDER BY u.firstname ASC, u.lastname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, new { ActiveBranchId = activeBranchId }).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<IReadOnlyList<DropdownDto>> GetReportingManagerDropdownAsync(CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "e", cancellationToken)
            .ConfigureAwait(false);

        var sql = $@"
            SELECT
                e.id AS Id,
                TRIM(u.firstname || ' ' || u.lastname) AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
            WHERE e.isactive = true{branchFilter}
            ORDER BY u.firstname ASC, u.lastname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, new { ActiveBranchId = activeBranchId }).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<bool> EmployeeCodeExistsAsync(
        string employeeCode,
        Guid branchId,
        Guid? excludingEmployeeId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
SELECT EXISTS (
    SELECT 1
    FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees}
    WHERE lower(employeecode) = lower(@EmployeeCode)
      AND branchid = @BranchId
      AND isactive = true
      AND (@ExcludingEmployeeId IS NULL OR id <> @ExcludingEmployeeId)
);
""";

        return await connection.QuerySingleAsync<bool>(
                sql,
                new
                {
                    EmployeeCode = employeeCode.Trim(),
                    BranchId = branchId,
                    ExcludingEmployeeId = excludingEmployeeId
                })
            .ConfigureAwait(false);
    }

    public async Task UpdateEmployeeAsync(EmployeeEntity employee, CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(employee, actorId, now);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            // Client update payloads often omit BranchId (defaults to Empty) — keep the stored branch.
            if (employee.BranchId == Guid.Empty)
            {
                var existingBranchId = await conn.ExecuteScalarAsync<Guid?>(
                    new CommandDefinition(
                        $@"SELECT branchid FROM {Context.OperationalSchema}.{DatabaseConfig.TableEmployees}
                           WHERE id = @Id AND isactive = true",
                        new { employee.Id },
                        transaction: tx,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (existingBranchId is null || existingBranchId == Guid.Empty)
                {
                    throw new InvalidOperationException("Employee not found.");
                }

                employee.BranchId = existingBranchId.Value;
            }
            else
            {
                employee.BranchId = await _branchWrite
                    .ResolveWriteBranchIdAsync(employee.BranchId, cancellationToken)
                    .ConfigureAwait(false);
            }

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
                    Now = SchoolLocalTime.NowDateTime(),
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

    private sealed class EmployeeDetailRow : EmployeeEntity
    {
        public Guid UserTypeId { get; set; }
    }

    private sealed class EmployeeListRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Designation { get; set; }
        public Guid UserTypeId { get; set; }
        public string? DepartmentName { get; set; }
        public string? ReportingManagerName { get; set; }
        public bool IsActive { get; set; }
    }
}
