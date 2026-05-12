using Dapper;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Common.Models;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Persistence.Repositories;

public abstract class BaseRepository
{
    protected readonly DapperContext Context;
    protected readonly ICurrentUserService CurrentUser;

    // Cache generated SQL per entity type to avoid repeated reflection
    private static readonly ConcurrentDictionary<string, string> _sqlCache = new();

    protected BaseRepository(DapperContext context, ICurrentUserService currentUser)
    {
        Context = context;
        CurrentUser = currentUser;
    }

    // ════════════════════════════════════════════════════════════
    // GENERIC INSERT
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates and executes an INSERT statement for the given entity.
    /// All properties (except [DbIgnore]) are included as columns.
    /// </summary>
    protected async Task<Guid> InsertAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null) where T : AuditableEntity
    {
        string sql = GetInsertSql<T>(schema, tableName);
        var id = await connection.ExecuteScalarAsync<Guid>(sql, entity, transaction);
        return id;
    }

    /// <summary>
    /// Generates and executes an INSERT statement (without RETURNING) for the given entity.
    /// </summary>
    protected async Task InsertWithoutReturnAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null) where T : AuditableEntity
    {
        string sql = GetInsertWithoutReturnSql<T>(schema, tableName);
        await connection.ExecuteAsync(sql, entity, transaction);
    }

    // ════════════════════════════════════════════════════════════
    // GENERIC UPDATE
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates and executes an UPDATE statement for the given entity.
    /// Updates all columns except [DbIgnore] and the key columns.
    /// Automatically increments versionno.
    /// Skips the update if no field has actually changed (dirty check).
    /// </summary>
    protected async Task<bool> UpdateAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null,
        params string[] keyColumns) where T : AuditableEntity
    {
        string[] keys = keyColumns.Length > 0 ? keyColumns : new[] { "Id" };

        // 1. Fetch the existing record for comparison
        var existing = await FetchExistingAsync<T>(connection, schema, tableName, entity, keys, transaction);
        if (existing == null) return false;

        // 2. Compare updatable fields — skip if nothing changed
        if (!HasChanges<T>(existing, entity, keys))
        {
            return false; // No update needed
        }

        // 3. Execute the update
        string sql = GetUpdateSql<T>(schema, tableName, keys);
        await connection.ExecuteAsync(sql, entity, transaction);
        return true;
    }

    // ════════════════════════════════════════════════════════════
    // GENERIC SOFT DELETE
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Soft-deletes a record by setting isactive = false.
    /// </summary>
    protected async Task SoftDeleteAsync(
        IDbConnection connection,
        string schema,
        string tableName,
        Guid id,
        IDbTransaction? transaction = null)
    {
        string sql = $"UPDATE {schema}.{tableName} SET isactive = false WHERE id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id }, transaction);
    }

    // ════════════════════════════════════════════════════════════
    // SQL GENERATION (with caching)
    // ════════════════════════════════════════════════════════════

    private static string GetInsertSql<T>(string schema, string tableName)
    {
        string cacheKey = $"INSERT_{typeof(T).FullName}_{schema}.{tableName}";
        return _sqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            string colNames = string.Join(", ", columns.Select(c => c.ToLowerInvariant()));
            string paramNames = string.Join(", ", columns.Select(c => $"@{c}"));
            return $"INSERT INTO {schema}.{tableName} ({colNames}) VALUES ({paramNames}) RETURNING id;";
        });
    }

    private static string GetInsertWithoutReturnSql<T>(string schema, string tableName)
    {
        string cacheKey = $"INSERT_NR_{typeof(T).FullName}_{schema}.{tableName}";
        return _sqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            string colNames = string.Join(", ", columns.Select(c => c.ToLowerInvariant()));
            string paramNames = string.Join(", ", columns.Select(c => $"@{c}"));
            return $"INSERT INTO {schema}.{tableName} ({colNames}) VALUES ({paramNames});";
        });
    }

    private static string GetUpdateSql<T>(string schema, string tableName, string[] keyColumns)
    {
        string keyStr = string.Join("_", keyColumns);
        string cacheKey = $"UPDATE_{typeof(T).FullName}_{schema}.{tableName}_{keyStr}";
        return _sqlCache.GetOrAdd(cacheKey, _ =>
        {
            var columns = GetDbColumns<T>();
            var keySet = new HashSet<string>(keyColumns, StringComparer.OrdinalIgnoreCase);

            // Fields that should never be modified during an UPDATE
            var excludeFromSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Id", "VersionNo", "CreatedBy", "CreatedOn", "IsActive"
            };
            // Also exclude all key columns
            foreach (var k in keyColumns) excludeFromSet.Add(k);

            var setCols = columns
                .Where(c => !excludeFromSet.Contains(c))
                .Select(c => $"{c.ToLowerInvariant()} = @{c}")
                .ToList();

            // Always increment versionno
            setCols.Add("versionno = versionno + 1");

            string setClause = string.Join(", ", setCols);
            string whereClause = string.Join(" AND ", keyColumns.Select(k => $"{k.ToLowerInvariant()} = @{k}"));

            return $"UPDATE {schema}.{tableName} SET {setClause} WHERE {whereClause} AND isactive = true;";
        });
    }

    /// <summary>
    /// Gets all property names from the entity type, excluding those marked with [DbIgnore].
    /// </summary>
    private static List<string> GetDbColumns<T>()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DbIgnoreAttribute>() == null)
            .Select(p => p.Name)
            .ToList();
    }

    // ════════════════════════════════════════════════════════════
    // DIRTY CHECK HELPERS
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Fetches the existing record from DB by key columns for dirty comparison.
    /// </summary>
    private static async Task<T?> FetchExistingAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        string[] keyColumns,
        IDbTransaction? transaction) where T : class
    {
        string whereClause = string.Join(" AND ", keyColumns.Select(k => $"{k.ToLowerInvariant()} = @{k}"));
        string sql = $"SELECT * FROM {schema}.{tableName} WHERE {whereClause} AND isactive = true LIMIT 1;";

        // Build a dynamic parameter object from key column values
        var parameters = new DynamicParameters();
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in keyColumns)
        {
            var prop = props.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (prop != null)
            {
                parameters.Add(key, prop.GetValue(entity));
            }
        }

        return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters, transaction);
    }

    /// <summary>
    /// Compares updatable fields between the existing DB record and the new entity.
    /// Returns true if at least one field has changed.
    /// </summary>
    private static bool HasChanges<T>(T existing, T updated, string[] keyColumns)
    {
        // Same exclusion set as GetUpdateSql
        var excludeFromCompare = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "VersionNo", "CreatedBy", "CreatedOn", "IsActive", "UpdatedBy", "UpdatedOn"
        };
        foreach (var k in keyColumns) excludeFromCompare.Add(k);

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
                return true; // At least one field changed
            }
        }

        return false; // Nothing changed
    }

    // ════════════════════════════════════════════════════════════
    // AUDIT HELPERS (existing)
    // ════════════════════════════════════════════════════════════

    protected void EnsureInsertAudit(AuditableEntity entity, DateTime utcNow, Guid? fallbackActorId = null)
    {
        Guid actor = ResolveInsertActor(fallbackActorId);

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

    protected async Task<PagedResult<T>> GetPagedResultAsync<T>(
        IDbConnection connection,
        string querySql,
        string countSql,
        object param,
        int pageIndex,
        int pageSize)
    {
        int totalCount = await connection.ExecuteScalarAsync<int>(countSql, param);

        int offset = (pageIndex - 1) * pageSize;
        string paginatedSql = $"{querySql} LIMIT {pageSize} OFFSET {offset}";

        var items = await connection.QueryAsync<T>(paginatedSql, param);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }
}
