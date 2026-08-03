using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.AcademicCalendar;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.AcademicCalendar.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.AcademicCalendar;

public sealed class AcademicCalendarRepository : BaseRepository, IAcademicCalendarRepository
{
    private readonly IBranchScopedWriteHelper _branchWrite;

    public AcademicCalendarRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _branchWrite = branchWrite;
    }

    public async Task<IReadOnlyList<CalendarEventTypeEntity>> GetEventTypesAsync(CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventTypes}
            WHERE isactive = true
            ORDER BY displayorder ASC, name ASC;
            """;
        var rows = await connection.QueryAsync<CalendarEventTypeEntity>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<CalendarEventTypeEntity?> GetEventTypeByIdAsync(
        Guid id,
        CancellationToken ct = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventTypes}
            WHERE id = @Id{activeFilter};
            """;
        return await connection.QuerySingleOrDefaultAsync<CalendarEventTypeEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> CreateEventTypeAsync(CalendarEventTypeEntity entity, CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(entity, utcNow);
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEventTypes, entity, tx)
                .ConfigureAwait(false);
            return entity.Id;
        }).ConfigureAwait(false);
    }

    public async Task UpdateEventTypeAsync(CalendarEventTypeEntity entity, CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        ApplyUpdateAudit(entity, ResolveUpdateActor(), utcNow);
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEventTypes, entity, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteEventTypeAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEventTypes, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<bool> EventTypeCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventTypes}
                WHERE lower(code) = lower(@Code) AND isactive = true
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Code = code, ExcludeId = excludeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> EventTypeInUseAsync(Guid eventTypeId, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEvents}
                WHERE eventtypeid = @EventTypeId AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { EventTypeId = eventTypeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<CalendarWeekendSettingEntity?> GetWeekendSettingsAsync(Guid branchId, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarWeekendSettings}
            WHERE branchid = @BranchId AND isactive = true;
            """;
        return await connection.QuerySingleOrDefaultAsync<CalendarWeekendSettingEntity>(
            new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid> UpsertWeekendSettingsAsync(CalendarWeekendSettingEntity entity, CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        entity.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(entity.BranchId, ct)
            .ConfigureAwait(false);

        var existing = await GetWeekendSettingsAsync(entity.BranchId, ct).ConfigureAwait(false);
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        if (existing is null)
        {
            if (entity.Id == Guid.Empty)
            {
                entity.Id = Guid.NewGuid();
            }

            EnsureInsertAudit(entity, utcNow);
            return await WithTransactionAsync(connection, async (conn, tx) =>
            {
                await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarWeekendSettings, entity, tx)
                    .ConfigureAwait(false);
                return entity.Id;
            }).ConfigureAwait(false);
        }

        entity.Id = existing.Id;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;
        entity.IsActive = true;
        ApplyUpdateAudit(entity, ResolveUpdateActor(), utcNow);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarWeekendSettings, entity, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsForRangeAsync(
        Guid branchId,
        Guid? academicYearId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT
                e.id AS Id,
                e.branchid AS BranchId,
                e.academicyearid AS AcademicYearId,
                e.eventtypeid AS EventTypeId,
                t.name AS EventTypeName,
                t.code AS EventTypeCode,
                e.title AS Title,
                e.description AS Description,
                e.startdate AS StartDate,
                e.enddate AS EndDate,
                e.appliestostudents AS AppliesToStudents,
                e.appliestoteachers AS AppliesToTeachers,
                e.appliestostaff AS AppliesToStaff,
                e.isnonworkingday AS IsNonWorkingDay,
                COALESCE(NULLIF(e.color, ''), t.color) AS Color,
                e.sourceexamid AS SourceExamId
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEvents} e
            INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventTypes} t ON t.id = e.eventtypeid
            WHERE e.isactive = true
              AND e.branchid = @BranchId
              AND e.startdate <= @To
              AND e.enddate >= @From
              AND (@AcademicYearId IS NULL OR e.academicyearid = @AcademicYearId)
            ORDER BY e.startdate ASC, e.title ASC;
            """;

        var rows = await connection.QueryAsync<CalendarEventDto>(
            new CommandDefinition(
                sql,
                new { BranchId = branchId, AcademicYearId = academicYearId, From = from, To = to },
                cancellationToken: ct))
            .ConfigureAwait(false);
        var list = rows.ToList();
        await AttachClassIdsAsync(connection, list, ct).ConfigureAwait(false);
        return list;
    }

    public async Task<CalendarEventEntity?> GetEventByIdAsync(
        Guid id,
        CancellationToken ct = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEvents}
            WHERE id = @Id{activeFilter};
            """;
        return await connection.QuerySingleOrDefaultAsync<CalendarEventEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetEventClassIdsAsync(Guid eventId, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT classid
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses}
            WHERE calendareventid = @EventId AND isactive = true;
            """;
        var rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { EventId = eventId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<CalendarEventEntity?> GetEventBySourceExamIdAsync(Guid sourceExamId, CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        var sql = $"""
            SELECT *
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEvents}
            WHERE sourceexamid = @SourceExamId AND isactive = true
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<CalendarEventEntity>(
            new CommandDefinition(sql, new { SourceExamId = sourceExamId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateEventAsync(
        CalendarEventEntity entity,
        IReadOnlyList<Guid> classIds,
        CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(entity, utcNow);
        entity.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(entity.BranchId, ct)
            .ConfigureAwait(false);

        var actorId = ResolveUpdateActor();
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEvents, entity, tx)
                .ConfigureAwait(false);
            await ReplaceEventClassesAsync(conn, tx, entity.Id, classIds, actorId, utcNow, ct).ConfigureAwait(false);
            return entity.Id;
        }).ConfigureAwait(false);
    }

    public async Task UpdateEventAsync(
        CalendarEventEntity entity,
        IReadOnlyList<Guid> classIds,
        CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(entity, actorId, utcNow);
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEvents, entity, tx, "Id")
                .ConfigureAwait(false);
            await ReplaceEventClassesAsync(conn, tx, entity.Id, classIds, actorId, utcNow, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        var utcNow = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string deactivateClasses = $"""
                UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses}
                SET isactive = false, updatedby = @ActorId, updatedon = @UtcNow, versionno = versionno + 1
                WHERE calendareventid = @EventId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    deactivateClasses,
                    new { EventId = id, ActorId = actorId, UtcNow = utcNow },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);
            await SoftDeleteAsync(conn, Context.OperationalSchema, DatabaseConfig.TableCalendarEvents, id, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DateOnly>> GetNonWorkingEventDatesAsync(
        Guid branchId,
        DateOnly from,
        DateOnly to,
        CalendarAudience audience,
        Guid? classId = null,
        CancellationToken ct = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string audienceColumn = audience switch
        {
            CalendarAudience.Students => "appliestostudents",
            CalendarAudience.Teachers => "appliestoteachers",
            CalendarAudience.Staff => "appliestostaff",
            _ => "appliestostaff"
        };

        // Students + classId: branch-wide (no class rows) OR explicitly linked to that class.
        // Students without classId: only branch-wide events (class-scoped holidays stay out of global student counts).
        string classFilter = audience == CalendarAudience.Students
            ? classId is null
                ? $"""
                  AND NOT EXISTS (
                      SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses} xc
                      WHERE xc.calendareventid = e.id AND xc.isactive = true)
                  """
                : $"""
                  AND (
                      NOT EXISTS (
                          SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses} xc
                          WHERE xc.calendareventid = e.id AND xc.isactive = true)
                      OR EXISTS (
                          SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses} xc
                          WHERE xc.calendareventid = e.id AND xc.classid = @ClassId AND xc.isactive = true)
                  )
                  """
            : string.Empty;

        var sql = $"""
            SELECT e.startdate AS StartDate, e.enddate AS EndDate
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEvents} e
            WHERE e.isactive = true
              AND e.branchid = @BranchId
              AND e.isnonworkingday = true
              AND e.{audienceColumn} = true
              AND e.startdate <= @To
              AND e.enddate >= @From
              {classFilter};
            """;

        var ranges = await connection.QueryAsync<(DateOnly StartDate, DateOnly EndDate)>(
            new CommandDefinition(
                sql,
                new { BranchId = branchId, From = from, To = to, ClassId = classId },
                cancellationToken: ct))
            .ConfigureAwait(false);

        var dates = new HashSet<DateOnly>();
        foreach (var (start, end) in ranges)
        {
            var cursor = start < from ? from : start;
            var last = end > to ? to : end;
            for (var d = cursor; d <= last; d = d.AddDays(1))
            {
                dates.Add(d);
            }
        }

        return dates.OrderBy(d => d).ToList();
    }

    private async Task AttachClassIdsAsync(
        System.Data.IDbConnection connection,
        List<CalendarEventDto> events,
        CancellationToken ct)
    {
        if (events.Count == 0)
        {
            return;
        }

        var ids = events.Select(e => e.Id).ToArray();
        var sql = $"""
            SELECT calendareventid AS EventId, classid AS ClassId
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses}
            WHERE isactive = true AND calendareventid = ANY(@Ids);
            """;
        var links = await connection.QueryAsync<(Guid EventId, Guid ClassId)>(
            new CommandDefinition(sql, new { Ids = ids }, cancellationToken: ct)).ConfigureAwait(false);

        var byEvent = links.GroupBy(x => x.EventId).ToDictionary(g => g.Key, g => g.Select(x => x.ClassId).ToList());
        foreach (var ev in events)
        {
            ev.ClassIds = byEvent.TryGetValue(ev.Id, out var classIds) ? classIds : [];
        }

        var allClassIds = events.SelectMany(e => e.ClassIds).Distinct().ToArray();
        if (allClassIds.Length == 0)
        {
            return;
        }

        var nameSql = $"""
            SELECT c.id AS ClassId, COALESCE({DashboardClassLabel.DisplayNameSql}, '') AS ClassName
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            WHERE c.id = ANY(@Ids) AND c.isactive = true;
            """;
        var names = await connection.QueryAsync<(Guid ClassId, string ClassName)>(
            new CommandDefinition(nameSql, new { Ids = allClassIds }, cancellationToken: ct)).ConfigureAwait(false);
        var nameById = names.ToDictionary(x => x.ClassId, x => x.ClassName);
        foreach (var ev in events)
        {
            ev.ClassNames = ev.ClassIds
                .Where(id => nameById.ContainsKey(id))
                .Select(id => nameById[id])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private async Task ReplaceEventClassesAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid eventId,
        IReadOnlyList<Guid> classIds,
        Guid actorId,
        DateTime utcNow,
        CancellationToken ct)
    {
        string deactivate = $"""
            UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses}
            SET isactive = false, updatedby = @ActorId, updatedon = @UtcNow, versionno = versionno + 1
            WHERE calendareventid = @EventId AND isactive = true;
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                deactivate,
                new { EventId = eventId, ActorId = actorId, UtcNow = utcNow },
                tx,
                cancellationToken: ct))
            .ConfigureAwait(false);

        foreach (Guid classId in classIds.Distinct())
        {
            string sql = $"""
                INSERT INTO {Context.OperationalSchema}.{DatabaseConfig.TableCalendarEventClasses}
                    (id, calendareventid, classid, isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (gen_random_uuid(), @EventId, @ClassId, true, 1, @ActorId, @UtcNow, @ActorId, @UtcNow)
                ON CONFLICT ON CONSTRAINT uq_calendareventclasses_event_class
                DO UPDATE SET isactive = true, updatedby = @ActorId, updatedon = @UtcNow,
                              versionno = {DatabaseConfig.TableCalendarEventClasses}.versionno + 1;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { EventId = eventId, ClassId = classId, ActorId = actorId, UtcNow = utcNow },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);
        }
    }
}
