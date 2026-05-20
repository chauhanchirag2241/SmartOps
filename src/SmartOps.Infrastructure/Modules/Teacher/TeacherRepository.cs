using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Modules.Teacher;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Teacher;

public sealed class TeacherRepository : BaseRepository, ITeacherRepository
{
    private readonly IUserScopeContext _scope;

    public TeacherRepository(DapperContext context, ICurrentUserService currentUser, IUserScopeContext scope)
        : base(context, currentUser)
    {
        _scope = scope;
    }

    public async Task<Guid> CreateTeacherAsync(TeacherEntity teacher, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (teacher.Id == Guid.Empty)
        {
            teacher.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(teacher, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var teacherId = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableTeachers, teacher, tx)
                .ConfigureAwait(false);
            return teacherId;
        }).ConfigureAwait(false);
    }

    public async Task<TeacherEntity?> GetTeacherByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableTeachers} WHERE id = @Id AND isactive = true";
        return await connection.QuerySingleOrDefaultAsync<TeacherEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task<PagedResult<TeacherListModel>> GetAllTeachersAsync(
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
                whereClause += " AND isactive = true";
                break;
            case StaffFilter.Inactive:
                whereClause += " AND isactive = false";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            whereClause += " AND (firstname ILIKE @SearchTerm OR lastname ILIKE @SearchTerm OR employeeid ILIKE @SearchTerm OR email ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedTeacherIds.Count > 0)
            {
                whereClause += " AND id = ANY(@ScopeTeacherIds)";
            }
            else if (_scope.AllowedDepartmentIds.Count > 0)
            {
                whereClause += " AND departmentid = ANY(@ScopeDepartmentIds)";
            }
            else
            {
                whereClause += " AND 1 = 0";
            }
        }

        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderBy = string.IsNullOrWhiteSpace(sortColumn) ? "createdon DESC" : $"{sortColumn} {direction}";

        var countSql = $"SELECT COUNT(*) FROM {Context.OperationalSchema}.{DatabaseConfig.TableTeachers} {whereClause}";
        var querySql = $@"
            SELECT 
                id, 
                TRIM(firstname || ' ' || lastname) AS Name, 
                email, 
                department AS Dept, 
                designation, 
                isactive 
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableTeachers} 
            {whereClause} 
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<TeacherListModel>(
            connection,
            querySql,
            countSql,
            new
            {
                SearchTerm = searchTerm,
                ScopeTeacherIds = _scope.AllowedTeacherIds.ToArray(),
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
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableTeachers}
            WHERE isactive = true
            ORDER BY firstname ASC, lastname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task UpdateTeacherAsync(TeacherEntity teacher, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(teacher, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableTeachers, teacher, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task SetTeacherUserIdAsync(Guid teacherId, Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableTeachers}
SET userid = @UserId, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
WHERE id = @TeacherId AND isactive = true
""";
        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    TeacherId = teacherId,
                    UserId = userId,
                    Now = DateTime.UtcNow,
                    Actor = ResolveUpdateActor()
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task DeleteTeacherAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableTeachers, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
