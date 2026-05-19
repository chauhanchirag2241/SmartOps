using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class.Enums;
using SmartOps.Domain.Modules.Class.Interfaces;
using SmartOps.Domain.Modules.Class.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;
using System.Data;

namespace SmartOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Class aggregate persistence. Same pattern as <see cref="StudentRepository"/>.
/// </summary>
public sealed class ClassRepository : BaseRepository, IClassRepository
{
    private readonly IUserScopeContext _scope;

    public ClassRepository(DapperContext context, ICurrentUserService currentUser, IUserScopeContext scope)
        : base(context, currentUser)
    {
        _scope = scope;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (classEntity.Id == Guid.Empty)
        {
            classEntity.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(classEntity, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var existingSql = $@"
            SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses}
            WHERE classname = @ClassName 
            AND section = @Section 
            AND streamgroup = @StreamGroup 
            AND academicyearid = @AcademicYearId 
            AND isactive = true;";

        var exists = await connection.ExecuteScalarAsync<int?>(existingSql, new 
        { 
            classEntity.ClassName, 
            classEntity.Section, 
            classEntity.StreamGroup, 
            classEntity.AcademicYearId 
        }).ConfigureAwait(false);

        if (exists.HasValue)
        {
            throw new InvalidOperationException("A class with the same name, section, stream/group and academic year already exists.");
        }

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var classId = await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableClasses, classEntity, tx)
                .ConfigureAwait(false);
            return classId;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ClassEntity?> GetClassByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
            SELECT * FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses}
            WHERE id = @Id AND isactive = true;";

        return await connection.QuerySingleOrDefaultAsync<ClassEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ClassListModel>> GetAllClassesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count > 0)
            {
                whereClause += " AND c.id = ANY(@ScopeClassIds)";
            }
            else
            {
                whereClause += " AND 1 = 0";
            }
        }

        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);

        var schema = Context.OperationalSchema;
        var table = DatabaseConfig.TableClasses;

        var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{table} c
            INNER JOIN {schema}.{DatabaseConfig.TableAcademicYears} ay ON c.academicyearid = ay.id
            {whereClause};";

        var querySql = $@"
            SELECT
                c.id AS Id,
                c.classname AS ClassName,
                CASE c.section
                    WHEN 1 THEN 'A'
                    WHEN 2 THEN 'B'
                    WHEN 3 THEN 'C'
                    WHEN 4 THEN 'D'
                    ELSE 'N/A'
                END AS Section,
                CASE c.streamgroup
                    WHEN 1 THEN 'None'
                    WHEN 2 THEN 'Science'
                    WHEN 3 THEN 'Commerce'
                    WHEN 4 THEN 'Arts'
                    WHEN 5 THEN 'Regional'
                    ELSE 'N/A'
                END AS StreamGroup,
                ay.title AS AcademicYear,
                c.capacity AS Capacity,
                COALESCE(c.classteacher, 'Not assigned') AS ClassTeacher,
                COALESCE(c.roomnumber, 'N/A') AS RoomNumber,
                CASE WHEN c.isactive THEN 'Active' ELSE 'Inactive' END AS Status,
                c.isactive AS IsActive
            FROM {schema}.{table} c
            INNER JOIN {schema}.{DatabaseConfig.TableAcademicYears} ay ON c.academicyearid = ay.id
            {whereClause}
            ORDER BY {orderBy}";

        var result = await GetPagedResultAsync<ClassListModel>(
                connection,
                querySql,
                countSql,
                new { SearchTerm = searchTerm, ScopeClassIds = _scope.AllowedClassIds.ToArray() },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        bool attendanceOnly = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        string whereClause = "WHERE c.isactive = true";
        object parameters = new { };

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            IReadOnlyList<Guid> scopeClassIds = attendanceOnly
                ? _scope.AllowedAttendanceClassIds
                : _scope.AllowedClassIds;

            if (scopeClassIds.Count > 0)
            {
                whereClause += " AND c.id = ANY(@ScopeClassIds)";
                parameters = new { ScopeClassIds = scopeClassIds.ToArray() };
            }
            else
            {
                return [];
            }
        }

        var sql = $@"
            SELECT
                c.id AS Id,
                c.classname ||
                    CASE c.section
                        WHEN 1 THEN ' - A'
                        WHEN 2 THEN ' - B'
                        WHEN 3 THEN ' - C'
                        WHEN 4 THEN ' - D'
                        ELSE ''
                    END AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c
            {whereClause}
            ORDER BY c.classname ASC, c.section ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, parameters).ConfigureAwait(false);
        return items.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(classEntity, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var existingSql = $@"
            SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses}
            WHERE classname = @ClassName 
            AND section = @Section 
            AND streamgroup = @StreamGroup 
            AND academicyearid = @AcademicYearId 
            AND id != @Id
            AND isactive = true;";

        var exists = await connection.ExecuteScalarAsync<int?>(existingSql, new 
        { 
            classEntity.ClassName, 
            classEntity.Section, 
            classEntity.StreamGroup, 
            classEntity.AcademicYearId,
            classEntity.Id
        }).ConfigureAwait(false);

        if (exists.HasValue)
        {
            throw new InvalidOperationException("Another class with the same name, section, stream/group and academic year already exists.");
        }

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableClasses, classEntity, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableClasses, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    #region List query helpers

    private static string BuildListWhereClause(ClassFilter filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        switch (filter)
        {
            case ClassFilter.Active:
                where += " AND c.isactive = true";
                break;
            case ClassFilter.Inactive:
                where += " AND c.isactive = false";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (c.classname ILIKE @SearchTerm OR c.classteacher ILIKE @SearchTerm OR c.roomnumber ILIKE @SearchTerm OR ay.title ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "c.createdon DESC, c.id ASC";
        }

        if (IsSortKey(sortColumn, "className"))
        {
            return $"c.classname {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "section"))
        {
            return $"c.section {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "streamGroup"))
        {
            return $"c.streamgroup {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "academicYear"))
        {
            return $"ay.title {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "capacity"))
        {
            return $"c.capacity {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "classTeacher"))
        {
            return $"c.classteacher {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "roomNumber"))
        {
            return $"c.roomnumber {direction}, c.id ASC";
        }

        return "c.createdon DESC, c.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
