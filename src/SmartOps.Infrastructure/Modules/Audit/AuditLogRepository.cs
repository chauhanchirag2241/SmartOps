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
        var globalSchema = DatabaseConfig.Schema_Global;
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

    private sealed class AuditLogRaw
    {
        public Guid Id { get; init; }
        public string Action { get; init; } = string.Empty;
        public Guid ChangedBy { get; init; }
        public string ChangedByName { get; init; } = string.Empty;
        public DateTime ChangedOn { get; init; }
        public string? ChangesJson { get; init; }
    }
}
