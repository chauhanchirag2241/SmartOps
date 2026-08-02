using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicCalendar;

namespace SmartOps.Application.Modules.AcademicCalendar.Interfaces;

public interface IAcademicCalendarService
{
    Task<Result<IReadOnlyList<CalendarEventTypeDto>>> GetEventTypesAsync(CancellationToken ct = default);
    Task<Result<CreateCalendarEventTypeResponse>> CreateEventTypeAsync(CreateCalendarEventTypeDto dto, CancellationToken ct = default);
    Task<Result> UpdateEventTypeAsync(Guid id, UpdateCalendarEventTypeDto dto, CancellationToken ct = default);
    Task<Result> DeleteEventTypeAsync(Guid id, CancellationToken ct = default);

    Task<Result<CalendarWeekendSettingDto>> GetWeekendSettingsAsync(CancellationToken ct = default);
    Task<Result<CalendarWeekendSettingDto>> UpsertWeekendSettingsAsync(UpsertCalendarWeekendSettingDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<CalendarEventDto>>> GetEventsForRangeAsync(
        Guid? academicYearId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
    Task<Result<CalendarEventDto>> GetEventByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<CreateCalendarEventResponse>> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken ct = default);
    Task<Result> UpdateEventAsync(Guid id, UpdateCalendarEventDto dto, CancellationToken ct = default);
    Task<Result> DeleteEventAsync(Guid id, CancellationToken ct = default);

    Task<bool> IsWorkingDayAsync(Guid? branchId, DateOnly date, CalendarAudience audience, CancellationToken ct = default, Guid? classId = null);
    Task<int> CountWorkingDaysAsync(Guid? branchId, int year, int month, CalendarAudience audience, CancellationToken ct = default, Guid? classId = null);
    Task<IReadOnlySet<int>> GetNonWorkingDayNumbersAsync(Guid? branchId, int year, int month, CalendarAudience audience, CancellationToken ct = default, Guid? classId = null);
    Task<Result<WorkingDaysResponseDto>> GetWorkingDaysAsync(int year, int month, CalendarAudience audience, CancellationToken ct = default);
}
