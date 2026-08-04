using System.Text.Json;
using Dapper;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Audit;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Models;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Audit;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly DapperContext _context;

    public AuditLogRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogListItemDto>> GetEntityHistoryAsync(
        string entityName,
        Guid entityId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var schema = _context.OperationalSchema;
        var table = DatabaseConfig.TableEntityAuditLogs;
        var globalSchema = _context.IdentitySchema;
        var usersTable = DatabaseConfig.TableUsers;

        var countSql = $"""
            SELECT COUNT(*)
            FROM "{schema}"."{table}" a
            WHERE a.entityname = @EntityName AND a.entityid = @EntityId;
            """;

        var querySql = $"""
            SELECT
                a.id         AS Id,
                a.action     AS Action,
                a.changedby  AS ChangedBy,
                COALESCE(
                    NULLIF(TRIM(u.username), ''),
                    NULLIF(TRIM(u.email), ''),
                    'System'
                ) AS ChangedByName,
                a.changedon  AS ChangedOn,
                a.changes::text AS ChangesJson
            FROM "{schema}"."{table}" a
            LEFT JOIN {globalSchema}.{usersTable} u ON u.id = a.changedby
            WHERE a.entityname = @EntityName AND a.entityid = @EntityId
            ORDER BY a.changedon DESC
            """;

        try
        {
            var connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

            var totalCount = await connection.ExecuteScalarAsync<int>(
                    countSql,
                    new { EntityName = entityName, EntityId = entityId })
                .ConfigureAwait(false);

            var offset = (pageIndex - 1) * pageSize;
            var paginatedSql = $"{querySql} LIMIT {pageSize} OFFSET {offset}";

            var rows = await connection.QueryAsync<AuditLogRaw>(
                    paginatedSql,
                    new { EntityName = entityName, EntityId = entityId })
                .ConfigureAwait(false);

            var items = rows.Select(r =>
            {
                var changes = ParseChanges(r.ChangesJson);
                return new AuditLogListItemDto
                {
                    Id = r.Id,
                    Action = r.Action,
                    ChangedBy = r.ChangedBy,
                    ChangedByName = r.ChangedByName,
                    ChangedOn = r.ChangedOn,
                    Changes = changes
                };
            }).ToList();

            items = await EnrichUserIdFieldValuesAsync(connection, items, globalSchema, usersTable)
                .ConfigureAwait(false);

            return new PagedResult<AuditLogListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return EmptyResult(pageIndex, pageSize);
        }
    }

    public async Task WriteAuditLogAsync(
        string entityName,
        Guid entityId,
        string action,
        Guid changedBy,
        DateTime changedOn,
        IReadOnlyList<FieldChangeDto> changes,
        CancellationToken cancellationToken = default)
    {
        var schema = _context.OperationalSchema;
        var table = DatabaseConfig.TableEntityAuditLogs;

        var changesJson = JsonSerializer.Serialize(changes);

        var sql = $"""
            INSERT INTO "{schema}"."{table}"
                (id, entityname, entityid, action, changedby, changedon, changes)
            VALUES
                (gen_random_uuid(), @EntityName, @EntityId, @Action, @ChangedBy, @ChangedOn, @Changes::jsonb);
            """;

        var connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(sql, new
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangedBy = changedBy,
            ChangedOn = changedOn,
            Changes = changesJson
        }).ConfigureAwait(false);
    }

    private static PagedResult<AuditLogListItemDto> EmptyResult(int pageIndex, int pageSize) =>
        new()
        {
            Items = [],
            TotalCount = 0,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

    private static IReadOnlyList<FieldChangeDto> ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<FieldChangeDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Replaces UserId GUID values in field changes with username (fallback email).
    /// </summary>
    private static async Task<List<AuditLogListItemDto>> EnrichUserIdFieldValuesAsync(
        System.Data.IDbConnection connection,
        List<AuditLogListItemDto> items,
        string identitySchema,
        string usersTable)
    {
        var userIds = new HashSet<Guid>();
        foreach (var item in items)
        {
            foreach (var change in item.Changes)
            {
                if (!IsUserIdField(change.Field))
                {
                    continue;
                }

                TryCollectGuid(change.OldValue, userIds);
                TryCollectGuid(change.NewValue, userIds);
            }
        }

        if (userIds.Count == 0)
        {
            return items;
        }

        var sql = $"""
            SELECT
                id AS Id,
                COALESCE(
                    NULLIF(TRIM(username), ''),
                    NULLIF(TRIM(email), ''),
                    id::text
                ) AS DisplayName
            FROM {identitySchema}.{usersTable}
            WHERE id = ANY(@Ids);
            """;

        var rows = await connection.QueryAsync<UserDisplayRow>(
                sql,
                new { Ids = userIds.ToArray() })
            .ConfigureAwait(false);

        var names = rows.ToDictionary(
            r => r.Id,
            r => r.DisplayName,
            EqualityComparer<Guid>.Default);

        return items.Select(item => new AuditLogListItemDto
        {
            Id = item.Id,
            Action = item.Action,
            ChangedBy = item.ChangedBy,
            ChangedByName = item.ChangedByName,
            ChangedOn = item.ChangedOn,
            Changes = MapUserIdChanges(item.Changes, names)
        }).ToList();
    }

    private static IReadOnlyList<FieldChangeDto> MapUserIdChanges(
        IReadOnlyList<FieldChangeDto> changes,
        IReadOnlyDictionary<Guid, string> userNames)
    {
        if (changes.Count == 0 || userNames.Count == 0)
        {
            return changes;
        }

        List<FieldChangeDto> mapped = new(changes.Count);
        foreach (var change in changes)
        {
            if (!IsUserIdField(change.Field))
            {
                mapped.Add(change);
                continue;
            }

            mapped.Add(new FieldChangeDto
            {
                Field = change.Field,
                OldValue = ResolveUserDisplay(change.OldValue, userNames),
                NewValue = ResolveUserDisplay(change.NewValue, userNames)
            });
        }

        return mapped;
    }

    private static bool IsUserIdField(string? field) =>
        string.Equals(field?.Trim(), "UserId", StringComparison.OrdinalIgnoreCase);

    private static void TryCollectGuid(string? raw, ISet<Guid> target)
    {
        if (Guid.TryParse(raw?.Trim(), out Guid id) && id != Guid.Empty)
        {
            target.Add(id);
        }
    }

    private static string? ResolveUserDisplay(string? raw, IReadOnlyDictionary<Guid, string> userNames)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (Guid.TryParse(raw.Trim(), out Guid id) &&
            userNames.TryGetValue(id, out string? name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return raw;
    }

    private sealed class AuditLogRaw
    {
        public Guid Id { get; init; }
        public string Action { get; init; } = string.Empty;
        public Guid ChangedBy { get; init; }
        public string ChangedByName { get; init; } = string.Empty;
        public DateTime ChangedOn { get; init; }
        public string? ChangesJson { get; init; }
    }

    private sealed class UserDisplayRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
    }
}
