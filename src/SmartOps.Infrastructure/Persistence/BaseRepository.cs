using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Text.Json;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Audit;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Persistence;

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
    /// Automatically writes a "Created" audit log if entity has [TrackHistory].
    /// </summary>
    protected async Task<Guid> InsertAsync<T>(
        IDbConnection connection,
        string schema,
        string tableName,
        T entity,
        IDbTransaction? transaction = null) where T : AuditableEntity
    {
        var sql = GetInsertSql<T>(schema, tableName);
        var id = await connection.ExecuteScalarAsync<Guid>(sql, entity, transaction).ConfigureAwait(false);

        // Audit: Created
        if (typeof(T).GetCustomAttribute<TrackHistoryAttribute>() is not null)
        {
            var entityId = GetEntityId(entity, id);
            var actor = GetAuditActor(entity);
            var allFields = GetAuditableFields<T>(entity, null);
            await WriteAuditLogInternalAsync(
                connection, schema, tableName, entityId,
                "Created", actor, entity.CreatedOn, allFields, transaction).ConfigureAwait(false);
        }

        return id;
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
    /// Automatically writes an "Updated" audit log if entity has [TrackHistory].
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

        var changedFields = GetChangedFields(existing, entity, keys);
        if (changedFields.Count == 0)
        {
            return false;
        }

        var sql = GetUpdateSql<T>(schema, tableName, keys);
        await connection.ExecuteAsync(sql, entity, transaction).ConfigureAwait(false);

        // Audit: Updated
        if (typeof(T).GetCustomAttribute<TrackHistoryAttribute>() is not null)
        {
            var entityId = GetEntityPrimaryId(entity, keys);
            var actor = entity.UpdatedBy != Guid.Empty ? entity.UpdatedBy : entity.CreatedBy;
            await WriteAuditLogInternalAsync(
                connection, schema, tableName, entityId,
                "Updated", actor, entity.UpdatedOn, changedFields, transaction).ConfigureAwait(false);
        }

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

        // Audit: Deleted (check if the entity type calling this tracks history)
        // We write a generic "Deleted" entry with no field diffs (actually we don't have T here directly, so we can't check [TrackHistory] cleanly).
        // Let's assume SoftDelete is used for main entities and just log it for all.
        await WriteAuditLogInternalAsync(
            connection, schema, tableName, id,
            "Deleted", actorId, utcNow,
            [new FieldChangeDto { Field = "IsActive", OldValue = "True", NewValue = "False" }],
            transaction)
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
        var columns = GetDbColumns<T>();
        var selectCols = string.Join(", ", columns.Select(c => c.ToLowerInvariant()));
        var whereClause = string.Join(" AND ", keyColumns.Select(k => $"{k.ToLowerInvariant()} = @{k}"));
        var sql = $"SELECT {selectCols} FROM {schema}.{tableName} WHERE {whereClause} AND isactive = true LIMIT 1;";

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
        return GetChangedFields(existing, updated, keyColumns).Count > 0;
    }

    private static List<FieldChangeDto> GetChangedFields<T>(T existing, T updated, string[] keyColumns)
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
            .Where(p => p.GetCustomAttribute<TrackHistoryIgnoreAttribute>() == null)
            .Where(p => !excludeFromCompare.Contains(p.Name));

        var changes = new List<FieldChangeDto>();

        foreach (var prop in props)
        {
            var oldVal = prop.GetValue(existing);
            var newVal = prop.GetValue(updated);
            if (!Equals(oldVal, newVal))
            {
                changes.Add(new FieldChangeDto
                {
                    Field = prop.Name,
                    OldValue = oldVal is null ? null : Convert.ToString(oldVal),
                    NewValue = newVal is null ? null : Convert.ToString(newVal)
                });
            }
        }

        return changes;
    }

    private static List<FieldChangeDto> GetAuditableFields<T>(T entity, T? ignoreEntity)
    {
        var excludeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "VersionNo", "CreatedBy", "CreatedOn", "IsActive", "UpdatedBy", "UpdatedOn",
        };

        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<DbIgnoreAttribute>() == null)
            .Where(p => p.GetCustomAttribute<TrackHistoryIgnoreAttribute>() == null)
            .Where(p => !excludeFields.Contains(p.Name));

        var fields = new List<FieldChangeDto>();
        foreach (var prop in props)
        {
            var val = prop.GetValue(entity);
            if (val is not null && !string.IsNullOrEmpty(Convert.ToString(val)))
            {
                fields.Add(new FieldChangeDto
                {
                    Field = prop.Name,
                    OldValue = null,
                    NewValue = Convert.ToString(val)
                });
            }
        }
        return fields;
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

    #region Audit log writer

    private protected static async Task WriteAuditLogInternalAsync(
        IDbConnection connection,
        string schema,
        string tableName,
        Guid entityId,
        string action,
        Guid changedBy,
        DateTime changedOn,
        IReadOnlyList<FieldChangeDto> changes,
        IDbTransaction? transaction = null)
    {
        try
        {
            if (transaction != null)
            {
                await connection.ExecuteAsync("SAVEPOINT audit_savepoint;", null, transaction).ConfigureAwait(false);
            }

            var auditTable = DatabaseConfig.TableEntityAuditLogs;
            var changesJson = JsonSerializer.Serialize(changes);
            var sql = $"""
                INSERT INTO {schema}.{auditTable}
                    (id, entityname, entityid, action, changedby, changedon, changes)
                VALUES
                    (gen_random_uuid(), @EntityName, @EntityId, @Action, @ChangedBy, @ChangedOn, @Changes::jsonb);
                """;
            await connection.ExecuteAsync(sql, new
            {
                EntityName = tableName,
                EntityId = entityId,
                Action = action,
                ChangedBy = changedBy,
                ChangedOn = changedOn,
                Changes = changesJson
            }, transaction).ConfigureAwait(false);

            if (transaction != null)
            {
                await connection.ExecuteAsync("RELEASE SAVEPOINT audit_savepoint;", null, transaction).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction != null)
            {
                try
                {
                    await connection.ExecuteAsync("ROLLBACK TO SAVEPOINT audit_savepoint;", null, transaction).ConfigureAwait(false);
                }
                catch { }
            }
            // Audit failures must not break the main operation
        }
    }

    private static Guid GetEntityId<T>(T entity, Guid fallback) where T : AuditableEntity
    {
        var idProp = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProp?.GetValue(entity) is Guid g && g != Guid.Empty)
        {
            return g;
        }
        return fallback;
    }

    private static Guid GetEntityPrimaryId<T>(T entity, string[] keyColumns) where T : AuditableEntity
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var key in keyColumns)
        {
            var prop = props.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (prop?.GetValue(entity) is Guid g) return g;
        }
        return Guid.Empty;
    }

    private static Guid GetAuditActor<T>(T entity) where T : AuditableEntity
    {
        return entity.CreatedBy != Guid.Empty ? entity.CreatedBy : Guid.Parse(DatabaseConfig.SystemUserId);
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
