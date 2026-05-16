using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Common.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared Dapper helpers: insert/update SQL cache, auditing, soft-delete, pagination, transaction scope.
/// Inherit per aggregate repository (e.g. <c>StudentRepository</c>).
/// </summary>
public abstract class BaseRepository
{
    private static readonly ConcurrentDictionary<string, string> SqlCache = new();

    protected readonly DapperContext Context;
    protected readonly ICurrentUserService CurrentUser;

    protected BaseRepository(DapperContext context, ICurrentUserService currentUser)
    {
        Context = context;
        CurrentUser = currentUser;
    }

    #region Transaction scope

    /// <summary>
    /// Runs <paramref name="action"/> inside a single DB transaction (commit on success, rollback on error).
    /// </summary>
    protected static async Task<TResult> WithTransactionAsync<TResult>(
        IDbConnection connection,
        Func<IDbConnection, IDbTransaction, Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);

        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await action(connection, transaction).ConfigureAwait(false);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>Transaction scope without a return value.</summary>
    protected static async Task WithTransactionAsync(
        IDbConnection connection,
        Func<IDbConnection, IDbTransaction, Task> action)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(action);

        using var transaction = connection.BeginTransaction();
        try
        {
            await action(connection, transaction).ConfigureAwait(false);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    #endregion

    #region Generic insert

    /// <summary>
    /// INSERT for <typeparamref name="T"/>; columns = all public properties except <see cref="DbIgnoreAttribute"/>.
    /// </summary>
    protected static async Task<Guid> InsertAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null) where T : AuditableEntity
    {
        var sql = GetInsertSql<T>(schema, tableName);
        return await connection.ExecuteScalarAsync<Guid>(sql, entity, transaction).ConfigureAwait(false);
    }

    /// <summary>INSERT without RETURNING id.</summary>
    protected static async Task InsertWithoutReturnAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null) where T : AuditableEntity
    {
        var sql = GetInsertWithoutReturnSql<T>(schema, tableName);
        await connection.ExecuteAsync(sql, entity, transaction).ConfigureAwait(false);
    }

    #endregion

    #region Generic update

    /// <summary>
    /// UPDATE with dirty check; increments <c>versionno</c>. Skips if no column changes vs DB row.
    /// </summary>
    protected static async Task<bool> UpdateAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        params string[] keyColumns) where T : AuditableEntity
    {
        var keys = keyColumns.Length > 0 ? keyColumns : new[] { "Id" };

        var existing = await FetchExistingAsync(connection, schema, tableName, entity, keys, transaction)
            .ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!HasChanges(existing, entity, keys))
        {
            return false;
        }

        var sql = GetUpdateSql<T>(schema, tableName, keys);
        await connection.ExecuteAsync(sql, entity, transaction).ConfigureAwait(false);
        return true;
    }

    #endregion

    #region Soft delete

    /// <summary>Sets <c>isactive = false</c> and audit columns on primary row.</summary>
    protected async Task SoftDeleteAsync(
        IDbConnection connection,
        string schema,
        string tableName,
        Guid id,
        IDbTransaction? transaction = null)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveInsertActor();

        var sql = $@"
            UPDATE {schema}.{tableName}
            SET isactive = false,
                updatedby = @ActorId,
                updatedon = @UtcNow,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;";

        await connection.ExecuteAsync(sql, new { Id = id, ActorId = actorId, UtcNow = utcNow }, transaction)
            .ConfigureAwait(false);
    }

    /// <summary>Soft-delete child rows by FK column (e.g. <c>studentid</c>).</summary>
    protected async Task SoftDeleteRelatedAsync(
        IDbConnection connection,
        string schema,
        string tableName,
        string foreignKeyColumn,
        Guid foreignKeyId,
        IDbTransaction? transaction = null)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveInsertActor();

        var fk = foreignKeyColumn.ToLowerInvariant();
        var sql = $@"
            UPDATE {schema}.{tableName}
            SET isactive = false,
                updatedby = @ActorId,
                updatedon = @UtcNow,
                versionno = versionno + 1
            WHERE {fk} = @Id AND isactive = true;";

        await connection.ExecuteAsync(sql, new { Id = foreignKeyId, ActorId = actorId, UtcNow = utcNow }, transaction)
            .ConfigureAwait(false);
    }

    #endregion

    #region SQL generation (cached)

    private static string GetInsertSql<T>(string schema, string tableName)
    {
        var cacheKey = $"INSERT_{typeof(T).FullName}_{schema}.{tableName}";
        return SqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            var colNames = string.Join(", ", columns.Select(c => c.ToLowerInvariant()));
            var paramNames = string.Join(", ", columns.Select(c => GetParameterExpression<T>(c)));
            return $"INSERT INTO {schema}.{tableName} ({colNames}) VALUES ({paramNames}) RETURNING id;";
        });
    }

    private static string GetInsertWithoutReturnSql<T>(string schema, string tableName)
    {
        var cacheKey = $"INSERT_NR_{typeof(T).FullName}_{schema}.{tableName}";
        return SqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            var colNames = string.Join(", ", columns.Select(c => c.ToLowerInvariant()));
            var paramNames = string.Join(", ", columns.Select(c => GetParameterExpression<T>(c)));
            return $"INSERT INTO {schema}.{tableName} ({colNames}) VALUES ({paramNames});";
        });
    }

    private static string GetUpdateSql<T>(string schema, string tableName, string[] keyColumns)
    {
        var keyStr = string.Join("_", keyColumns);
        var cacheKey = $"UPDATE_{typeof(T).FullName}_{schema}.{tableName}_{keyStr}";
        return SqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            var keySet = new HashSet<string>(keyColumns, StringComparer.OrdinalIgnoreCase);

            var excludeFromSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Id", "VersionNo", "CreatedBy", "CreatedOn", "IsActive",
            };
            foreach (var k in keyColumns)
            {
                excludeFromSet.Add(k);
            }

            var setCols = columns
                .Where(c => !excludeFromSet.Contains(c))
                .Select(c => $"{c.ToLowerInvariant()} = {GetParameterExpression<T>(c)}")
                .ToList();

            setCols.Add("versionno = versionno + 1");

            var setClause = string.Join(", ", setCols);
            var whereClause = string.Join(" AND ", keyColumns.Select(k => $"{k.ToLowerInvariant()} = @{k}"));

            return $"UPDATE {schema}.{tableName} SET {setClause} WHERE {whereClause} AND isactive = true;";
        });
    }

    private static List<string> GetDbColumns<T>()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DbIgnoreAttribute>() == null)
            .Select(p => p.Name)
            .ToList();
    }

    private static string GetParameterExpression<T>(string columnName)
    {
        var property = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        return property?.GetCustomAttribute<DbJsonbAttribute>() is not null
            ? $"@{columnName}::jsonb"
            : $"@{columnName}";
    }

    #endregion

    #region Dirty check

    private static async Task<T?> FetchExistingAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        string[] keyColumns,
        IDbTransaction? transaction) where T : class
    {
        var whereClause = string.Join(" AND ", keyColumns.Select(k => $"{k.ToLowerInvariant()} = @{k}"));
        var sql = $"SELECT * FROM {schema}.{tableName} WHERE {whereClause} AND isactive = true LIMIT 1;";

        var parameters = new DynamicParameters();
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in keyColumns)
        {
            var prop = props.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (prop is not null)
            {
                parameters.Add(key, prop.GetValue(entity));
            }
        }

        return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters, transaction).ConfigureAwait(false);
    }

    private static bool HasChanges<T>(T existing, T updated, string[] keyColumns)
    {
        var excludeFromCompare = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "VersionNo", "CreatedBy", "CreatedOn", "IsActive", "UpdatedBy", "UpdatedOn",
        };
        foreach (var k in keyColumns)
        {
            excludeFromCompare.Add(k);
        }

        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DbIgnoreAttribute>() == null)
            .Where(p => !excludeFromCompare.Contains(p.Name));

        foreach (var prop in props)
        {
            var oldVal = prop.GetValue(existing);
            var newVal = prop.GetValue(updated);
            if (!Equals(oldVal, newVal))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Audit

    protected void EnsureInsertAudit(AuditableEntity entity, DateTime utcNow, Guid? fallbackActorId = null)
    {
        var actor = ResolveInsertActor(fallbackActorId);

        if (entity.CreatedBy == Guid.Empty)
        {
            entity.CreatedBy = actor;
        }

        if (entity.UpdatedBy == Guid.Empty)
        {
            entity.UpdatedBy = entity.CreatedBy;
        }

        entity.CreatedOn = utcNow;
        entity.UpdatedOn = utcNow;
        entity.IsActive = true;
        entity.VersionNo = 1;
    }

    protected Guid ResolveInsertActor(Guid? fallbackActorId = null)
    {
        if (CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty)
        {
            return CurrentUser.UserId;
        }

        if (fallbackActorId.HasValue && fallbackActorId.Value != Guid.Empty)
        {
            return fallbackActorId.Value;
        }

        return Guid.Parse(DatabaseConfig.SystemUserId);
    }

    protected Guid ResolveUpdateActor(Guid? fallbackUserId = null)
    {
        if (CurrentUser.IsAuthenticated && CurrentUser.UserId != Guid.Empty)
        {
            return CurrentUser.UserId;
        }

        if (fallbackUserId.HasValue && fallbackUserId.Value != Guid.Empty)
        {
            return fallbackUserId.Value;
        }

        throw new InvalidOperationException("An actor is required for updates.");
    }

    protected static void ApplyUpdateAudit(AuditableEntity entity, Guid actorId, DateTime utcNow)
    {
        entity.UpdatedBy = actorId;
        entity.UpdatedOn = utcNow;
    }

    #endregion

    #region Pagination

    protected static async Task<PagedResult<T>> GetPagedResultAsync<T>(
        IDbConnection connection,
        string querySql,
        string countSql,
        object param,
        int pageIndex,
        int pageSize)
    {
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, param).ConfigureAwait(false);

        var offset = (pageIndex - 1) * pageSize;
        var paginatedSql = $"{querySql} LIMIT {pageSize} OFFSET {offset}";

        var items = await connection.QueryAsync<T>(paginatedSql, param).ConfigureAwait(false);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    #endregion
}
