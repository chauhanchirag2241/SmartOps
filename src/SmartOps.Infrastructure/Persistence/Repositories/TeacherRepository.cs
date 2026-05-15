using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Persistence.Repositories;

public sealed class TeacherRepository : BaseRepository, ITeacherRepository
{
    public TeacherRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
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
            var teacherId = await InsertAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableTeachers, teacher, tx)
                .ConfigureAwait(false);
            return teacherId;
        }).ConfigureAwait(false);
    }

    public async Task<TeacherEntity?> GetTeacherByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableTeachers} WHERE id = @Id AND isactive = true";
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

        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var orderBy = string.IsNullOrWhiteSpace(sortColumn) ? "createdon DESC" : $"{sortColumn} {direction}";

        var countSql = $"SELECT COUNT(*) FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableTeachers} {whereClause}";
        var querySql = $@"
            SELECT 
                id, 
                TRIM(firstname || ' ' || lastname) AS Name, 
                email, 
                department AS Dept, 
                designation, 
                isactive 
            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableTeachers} 
            {whereClause} 
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<TeacherListModel>(
            connection,
            querySql,
            countSql,
            new { SearchTerm = searchTerm },
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
            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableTeachers}
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
            await UpdateAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableTeachers, teacher, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteTeacherAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableTeachers, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
