using SmartOps.Application.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.AcademicCalendar.Entities;

namespace SmartOps.Application.Modules.AcademicCalendar.Interfaces;

public interface IAcademicCalendarRepository
{
    // Event types
    Task<IReadOnlyList<CalendarEventTypeEntity>> GetEventTypesAsync(CancellationToken ct = default);
    Task<CalendarEventTypeEntity?> GetEventTypeByIdAsync(Guid id, CancellationToken ct = default, bool includeInactive = false);
    Task<Guid> CreateEventTypeAsync(CalendarEventTypeEntity entity, CancellationToken ct = default);
    Task UpdateEventTypeAsync(CalendarEventTypeEntity entity, CancellationToken ct = default);
    Task DeleteEventTypeAsync(Guid id, CancellationToken ct = default);
    Task<bool> EventTypeCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> EventTypeInUseAsync(Guid eventTypeId, CancellationToken ct = default);

    // Weekend settings
    Task<CalendarWeekendSettingEntity?> GetWeekendSettingsAsync(Guid branchId, CancellationToken ct = default);
    Task<Guid> UpsertWeekendSettingsAsync(CalendarWeekendSettingEntity entity, CancellationToken ct = default);

    // Events
    Task<IReadOnlyList<CalendarEventDto>> GetEventsForRangeAsync(
        Guid branchId,
        Guid? academicYearId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
    Task<CalendarEventEntity?> GetEventByIdAsync(Guid id, CancellationToken ct = default, bool includeInactive = false);
    Task<Guid> CreateEventAsync(CalendarEventEntity entity, IReadOnlyList<Guid> classIds, CancellationToken ct = default);
    Task UpdateEventAsync(CalendarEventEntity entity, IReadOnlyList<Guid> classIds, CancellationToken ct = default);
    Task DeleteEventAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetEventClassIdsAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<DateOnly>> GetNonWorkingEventDatesAsync(
        Guid branchId,
        DateOnly from,
        DateOnly to,
        CalendarAudience audience,
        Guid? classId = null,
        CancellationToken ct = default);
}
