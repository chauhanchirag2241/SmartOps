using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Timetable;
using SmartOps.Domain.Modules.Timetable.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Timetable;

public sealed class TimetableRepository : BaseRepository, ITimetableRepository
{
    public TimetableRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    private string Schema => Context.OperationalSchema;

    public async Task<ClassTimetableEntity?> GetTimetableByIdAsync(Guid id, CancellationToken cancellationToken, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var sql = $@"
SELECT id AS Id, academicyearid AS AcademicYearId, classid AS ClassId,
       periodtemplateid AS PeriodTemplateId,
       effectivefrom AS EffectiveFrom, notes AS Notes,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassTimetables}
WHERE id = @Id{activeFilter};";
        return await connection.QuerySingleOrDefaultAsync<ClassTimetableEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassTimetableEntity>> GetVersionsAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT id AS Id, academicyearid AS AcademicYearId, classid AS ClassId,
       periodtemplateid AS PeriodTemplateId,
       effectivefrom AS EffectiveFrom, notes AS Notes,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassTimetables}
WHERE classid = @ClassId AND academicyearid = @AcademicYearId AND isactive = true
ORDER BY effectivefrom DESC;";
        var rows = await connection.QueryAsync<ClassTimetableEntity>(sql, new { ClassId = classId, AcademicYearId = academicYearId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ClassTimetableEntity?> GetCurrentVersionAsync(Guid classId, Guid academicYearId, DateOnly asOf, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT id AS Id, academicyearid AS AcademicYearId, classid AS ClassId,
       periodtemplateid AS PeriodTemplateId,
       effectivefrom AS EffectiveFrom, notes AS Notes,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassTimetables}
WHERE classid = @ClassId AND academicyearid = @AcademicYearId
  AND isactive = true AND effectivefrom <= @AsOf
ORDER BY effectivefrom DESC
LIMIT 1;";
        return await connection.QuerySingleOrDefaultAsync<ClassTimetableEntity>(
            sql, new { ClassId = classId, AcademicYearId = academicYearId, AsOf = asOf }).ConfigureAwait(false);
    }

    public async Task<Guid> CreateTimetableAsync(ClassTimetableEntity entity, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        EnsureInsertAudit(entity, utcNow);
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, Schema, DatabaseConfig.TableClassTimetables, entity, tx).ConfigureAwait(false);
            return entity.Id;
        }).ConfigureAwait(false);
    }

    public async Task UpdateTimetableAsync(ClassTimetableEntity entity, CancellationToken cancellationToken)
    {
        ApplyUpdateAudit(entity, ResolveUpdateActor(), DateTime.UtcNow);
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await UpdateAsync(conn, Schema, DatabaseConfig.TableClassTimetables, entity, tx, "Id").ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeleteTimetableAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteRelatedAsync(conn, Schema, DatabaseConfig.TableClassTimetableSlots, "timetableid", id, tx)
                .ConfigureAwait(false);
            await SoftDeleteAsync(conn, Schema, DatabaseConfig.TableClassTimetables, id, tx).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassTimetableSlotEntity>> GetSlotsByTimetableIdAsync(Guid timetableId, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT id AS Id, timetableid AS TimetableId, dayofweek AS DayOfWeek, periodid AS PeriodId,
       subjectid AS SubjectId, employeeid AS EmployeeId, roomno AS RoomNo,
       isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {Schema}.{DatabaseConfig.TableClassTimetableSlots}
WHERE timetableid = @TimetableId AND isactive = true;";
        var rows = await connection.QueryAsync<ClassTimetableSlotEntity>(sql, new { TimetableId = timetableId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TimetableSlotDetailRow>> GetSlotDetailsByTimetableIdAsync(
        Guid timetableId,
        CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Keep this query lean — only enrich slot subject/teacher names (periods come from template).
        var sql = $@"
SELECT
    s.id AS SlotId,
    s.timetableid AS TimetableId,
    s.dayofweek AS DayOfWeek,
    s.periodid AS PeriodId,
    s.subjectid AS SubjectId,
    sub.subjectname AS SubjectName,
    sub.subjectcode AS SubjectCode,
    s.employeeid AS EmployeeId,
    NULLIF(TRIM(CONCAT(COALESCE(e.firstname, ''), ' ', COALESCE(e.lastname, ''))), '') AS EmployeeName,
    s.roomno AS RoomNo
FROM {Schema}.{DatabaseConfig.TableClassTimetableSlots} s
LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} sub ON sub.id = s.subjectid
LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = s.employeeid
WHERE s.timetableid = @TimetableId AND s.isactive = true;";
        var rows = await connection.QueryAsync<TimetableSlotDetailRow>(sql, new { TimetableId = timetableId })
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task ReplaceSlotsAsync(Guid timetableId, IReadOnlyList<ClassTimetableSlotEntity> slots, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var actor = ResolveUpdateActor();
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await conn.ExecuteAsync(new CommandDefinition($@"
UPDATE {Schema}.{DatabaseConfig.TableClassTimetableSlots}
SET isactive = false, versionno = versionno + 1, updatedby = @Actor, updatedon = @Now
WHERE timetableid = @TimetableId AND isactive = true;",
                new { TimetableId = timetableId, Actor = actor, Now = utcNow }, tx, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            foreach (var slot in slots)
            {
                if (slot.Id == Guid.Empty) slot.Id = Guid.NewGuid();
                slot.TimetableId = timetableId;
                EnsureInsertAudit(slot, utcNow);
                await InsertAsync(conn, Schema, DatabaseConfig.TableClassTimetableSlots, slot, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<TimetableConflictRow>> FindTeacherConflictsAsync(
        Guid academicYearId,
        Guid excludeTimetableId,
        DateOnly effectiveFrom,
        IReadOnlyList<TimetableSlotConflictKey> keys,
        CancellationToken cancellationToken)
        => FindConflictsAsync(academicYearId, excludeTimetableId, effectiveFrom, keys, roomMode: false, cancellationToken);

    public Task<IReadOnlyList<TimetableConflictRow>> FindRoomConflictsAsync(
        Guid academicYearId,
        Guid excludeTimetableId,
        DateOnly effectiveFrom,
        IReadOnlyList<TimetableSlotConflictKey> keys,
        CancellationToken cancellationToken)
        => FindConflictsAsync(academicYearId, excludeTimetableId, effectiveFrom, keys, roomMode: true, cancellationToken);

    private async Task<IReadOnlyList<TimetableConflictRow>> FindConflictsAsync(
        Guid academicYearId,
        Guid excludeTimetableId,
        DateOnly effectiveFrom,
        IReadOnlyList<TimetableSlotConflictKey> keys,
        bool roomMode,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0) return [];

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"
WITH ranked AS (
    SELECT t.id, t.classid, t.effectivefrom,
           LEAD(t.effectivefrom) OVER (PARTITION BY t.classid ORDER BY t.effectivefrom) AS nextfrom
    FROM {Schema}.{DatabaseConfig.TableClassTimetables} t
    WHERE t.academicyearid = @AcademicYearId AND t.isactive = true
),
self_row AS (
    SELECT classid, effectivefrom,
           LEAD(effectivefrom) OVER (PARTITION BY classid ORDER BY effectivefrom) AS nextfrom
    FROM ranked WHERE id = @ExcludeTimetableId
    UNION ALL
    SELECT (SELECT classid FROM {Schema}.{DatabaseConfig.TableClassTimetables} WHERE id = @ExcludeTimetableId AND isactive = true),
           @EffectiveFrom::date,
           NULL::date
    WHERE NOT EXISTS (SELECT 1 FROM ranked WHERE id = @ExcludeTimetableId)
),
self_win AS (
    SELECT classid, effectivefrom AS startfrom, COALESCE(nextfrom, '9999-12-31'::date) AS endfrom
    FROM self_row
    LIMIT 1
)
SELECT
    s.timetableid AS TimetableId,
    r.classid AS ClassId,
    c.classname AS ClassName,
    r.effectivefrom AS EffectiveFrom,
    s.dayofweek AS DayOfWeek,
    s.periodid AS PeriodId,
    p.name AS PeriodName,
    s.employeeid AS EmployeeId,
    NULLIF(TRIM(CONCAT(COALESCE(e.firstname, ''), ' ', COALESCE(e.lastname, ''))), '') AS EmployeeName,
    s.roomno AS RoomNo,
    s.subjectid AS SubjectId,
    sub.subjectname AS SubjectName
FROM ranked r
INNER JOIN {Schema}.{DatabaseConfig.TableClassTimetableSlots} s ON s.timetableid = r.id AND s.isactive = true
CROSS JOIN self_win w
LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = r.classid
LEFT JOIN {Schema}.{DatabaseConfig.TablePeriods} p ON p.id = s.periodid
LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = s.employeeid
LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} sub ON sub.id = s.subjectid
WHERE r.id <> @ExcludeTimetableId
  AND r.effectivefrom < w.endfrom
  AND COALESCE(r.nextfrom, '9999-12-31'::date) > w.startfrom;";

        var candidates = (await connection.QueryAsync<TimetableConflictRow>(sql, new
        {
            AcademicYearId = academicYearId,
            ExcludeTimetableId = excludeTimetableId == Guid.Empty ? Guid.NewGuid() : excludeTimetableId,
            EffectiveFrom = effectiveFrom,
        }).ConfigureAwait(false)).ToList();

        var result = new List<TimetableConflictRow>();
        foreach (var key in keys)
        {
            foreach (var row in candidates)
            {
                if (row.DayOfWeek != key.DayOfWeek || row.PeriodId != key.PeriodId) continue;

                if (roomMode)
                {
                    if (string.IsNullOrWhiteSpace(key.RoomNo) || string.IsNullOrWhiteSpace(row.RoomNo)) continue;
                    if (!string.Equals(key.RoomNo.Trim(), row.RoomNo.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                }
                else
                {
                    if (!key.EmployeeId.HasValue || !row.EmployeeId.HasValue) continue;
                    if (key.EmployeeId.Value != row.EmployeeId.Value) continue;
                }

                result.Add(row);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<TimetableSlotDetailRow>> GetSlotsForTeacherAsync(
        Guid academicYearId,
        Guid employeeId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
WITH current_versions AS (
    SELECT DISTINCT ON (t.classid)
        t.id, t.classid, t.effectivefrom
    FROM {Schema}.{DatabaseConfig.TableClassTimetables} t
    WHERE t.academicyearid = @AcademicYearId
      AND t.isactive = true
      AND t.effectivefrom <= @AsOf
    ORDER BY t.classid, t.effectivefrom DESC
)
SELECT
    s.id AS SlotId,
    s.timetableid AS TimetableId,
    cv.classid AS ClassId,
    c.classname AS ClassName,
    cv.effectivefrom AS EffectiveFrom,
    s.dayofweek AS DayOfWeek,
    s.periodid AS PeriodId,
    p.name AS PeriodName,
    p.shortname AS PeriodShortName,
    p.periodorder AS PeriodOrder,
    p.starttime AS StartTime,
    p.endtime AS EndTime,
    p.isbreak AS IsBreak,
    s.subjectid AS SubjectId,
    sub.subjectname AS SubjectName,
    sub.subjectcode AS SubjectCode,
    s.employeeid AS EmployeeId,
    NULLIF(TRIM(CONCAT(COALESCE(e.firstname, ''), ' ', COALESCE(e.lastname, ''))), '') AS EmployeeName,
    s.roomno AS RoomNo
FROM current_versions cv
INNER JOIN {Schema}.{DatabaseConfig.TableClassTimetableSlots} s
    ON s.timetableid = cv.id AND s.isactive = true AND s.employeeid = @EmployeeId
LEFT JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = cv.classid
LEFT JOIN {Schema}.{DatabaseConfig.TablePeriods} p ON p.id = s.periodid
LEFT JOIN {Schema}.{DatabaseConfig.TableSubjects} sub ON sub.id = s.subjectid
LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = s.employeeid
ORDER BY p.periodorder, s.dayofweek;";

        var rows = await connection.QueryAsync<TimetableSlotDetailRow>(sql, new
        {
            AcademicYearId = academicYearId,
            EmployeeId = employeeId,
            AsOf = asOf,
        }).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT id FROM {Schema}.{DatabaseConfig.TableEmployees}
WHERE userid = @UserId AND isactive = true LIMIT 1;";
        return await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { UserId = userId }).ConfigureAwait(false);
    }

    public async Task<Guid?> GetStudentIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT id FROM {Schema}.{DatabaseConfig.TableStudents}
WHERE userid = @UserId AND isactive = true LIMIT 1;";
        return await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { UserId = userId }).ConfigureAwait(false);
    }

    public async Task<Guid?> GetStudentClassIdAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $@"
SELECT classid FROM {Schema}.{DatabaseConfig.TableStudentAcademics}
WHERE studentid = @StudentId AND academicyearid = @AcademicYearId AND isactive = true
LIMIT 1;";
        return await connection.QuerySingleOrDefaultAsync<Guid?>(sql, new { StudentId = studentId, AcademicYearId = academicYearId })
            .ConfigureAwait(false);
    }
}
