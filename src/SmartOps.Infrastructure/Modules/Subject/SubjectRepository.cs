using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Subject.Entities;
using SmartOps.Domain.Modules.Subject;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Subject;

public sealed class SubjectRepository : BaseRepository, ISubjectRepository
{
    private readonly IUserScopeContext _scope;

    public SubjectRepository(DapperContext context, ICurrentUserService currentUser, IUserScopeContext scope)
        : base(context, currentUser)
    {
        _scope = scope;
    }

    public async Task<Guid> CreateSubjectAsync(SubjectEntity subject, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        if (subject.Id == Guid.Empty)
        {
            subject.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(subject, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableSubjects, subject, tx)
                .ConfigureAwait(false);
            return subject.Id;
        }).ConfigureAwait(false);
    }

    public async Task<PagedResult<SubjectListModel>> GetAllSubjectsAsync(
        int pageIndex, 
        int pageSize, 
        string? searchTerm, 
        string? sortColumn, 
        string? sortDirection, 
        string? filter, 
        CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedSubjectIds.Count > 0)
            {
                whereClause += " AND s.id = ANY(@ScopeSubjectIds)";
            }
            else
            {
                whereClause += " AND 1 = 0";
            }
        }

        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableSubjects;

        var countSql = $@"SELECT COUNT(*) FROM {schema}.{table} s {whereClause};";

        var querySql = $@"
            SELECT
                s.id AS Id,
                s.subjectname AS SubjectName,
                s.subjectcode AS SubjectCode,
                CASE s.subjecttype
                    WHEN 1 THEN 'Theory'
                    WHEN 2 THEN 'Practical'
                    WHEN 3 THEN 'Both'
                    ELSE 'N/A'
                END AS SubjectType,
                CASE s.subjectcategory
                    WHEN 1 THEN 'Core'
                    WHEN 2 THEN 'Elective'
                    WHEN 3 THEN 'Co-curricular'
                    ELSE 'N/A'
                END AS SubjectCategory,
                CASE s.medium
                    WHEN 1 THEN 'English'
                    WHEN 2 THEN 'Hindi'
                    WHEN 3 THEN 'Gujarati'
                    ELSE 'N/A'
                END AS Medium,
                s.isactive AS IsActive
            FROM {schema}.{table} s
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<SubjectListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm, ScopeSubjectIds = _scope.AllowedSubjectIds.ToArray() },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DropdownDto>> GetSubjectDropdownAsync(CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        string whereClause = "WHERE s.isactive = true";
        object parameters = new { };

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedSubjectIds.Count > 0)
            {
                whereClause += " AND s.id = ANY(@ScopeSubjectIds)";
                parameters = new { ScopeSubjectIds = _scope.AllowedSubjectIds.ToArray() };
            }
            else
            {
                return [];
            }
        }

        var sql = $@"
            SELECT
                s.id AS Id,
                s.subjectname AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableSubjects} s
            {whereClause}
            ORDER BY s.subjectname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, parameters).ConfigureAwait(false);
        return items.ToList();
    }

    public async Task<SubjectEntity?> GetSubjectByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableSubjects}
            WHERE id = @Id AND isactive = true;";

        return await connection.QuerySingleOrDefaultAsync<SubjectEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task UpdateSubjectAsync(SubjectEntity subject, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(subject, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableSubjects, subject, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteSubjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableSubjects, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static string BuildListWhereClause(string? filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        if (!string.IsNullOrWhiteSpace(filter) && filter != "All")
        {
            if (filter == "Active") where += " AND s.isactive = true";
            else if (filter == "Inactive") where += " AND s.isactive = false";
            else
            {
                // Assuming it's a category filter if not active/inactive
                where += " AND CASE s.subjectcategory WHEN 1 THEN 'Core' WHEN 2 THEN 'Elective' WHEN 3 THEN 'Co-curricular' ELSE '' END = @Filter";
            }
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (s.subjectname ILIKE @SearchTerm OR s.subjectcode ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "s.createdon DESC, s.id ASC";
        }

        return sortColumn.ToLower() switch
        {
            "subjectname" => $"s.subjectname {direction}, s.id ASC",
            "subjectcode" => $"s.subjectcode {direction}, s.id ASC",
            "subjecttype" => $"s.subjecttype {direction}, s.id ASC",
            _ => "s.createdon DESC, s.id ASC"
        };
    }
}
