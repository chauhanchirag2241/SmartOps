using SmartOps.Domain.Modules.AcademicCalendar.Entities;

namespace SmartOps.Application.Modules.AcademicCalendar;

public class CreateCalendarEventTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Color { get; set; } = "#5B8DEF";
    public bool IsNonWorkingDefault { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class UpdateCalendarEventTypeDto : CreateCalendarEventTypeDto
{
}

public sealed class CalendarEventTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Color { get; set; } = "#5B8DEF";
    public bool IsNonWorkingDefault { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpsertCalendarWeekendSettingDto
{
    public bool SundayOff { get; set; } = true;
    public bool SaturdayOff { get; set; }
    public bool MondayOff { get; set; }
    public bool TuesdayOff { get; set; }
    public bool WednesdayOff { get; set; }
    public bool ThursdayOff { get; set; }
    public bool FridayOff { get; set; }
}

public sealed class CalendarWeekendSettingDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public bool SundayOff { get; set; }
    public bool SaturdayOff { get; set; }
    public bool MondayOff { get; set; }
    public bool TuesdayOff { get; set; }
    public bool WednesdayOff { get; set; }
    public bool ThursdayOff { get; set; }
    public bool FridayOff { get; set; }
}

public class CreateCalendarEventDto
{
    public Guid AcademicYearId { get; set; }
    public Guid EventTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool AppliesToStudents { get; set; } = true;
    public bool AppliesToTeachers { get; set; } = true;
    public bool AppliesToStaff { get; set; } = true;
    public bool IsNonWorkingDay { get; set; }
    public string? Color { get; set; }
    /// <summary>
    /// When non-empty, student audience is limited to these classes (not all students).
    /// </summary>
    public List<Guid> ClassIds { get; set; } = [];
}

public sealed class UpdateCalendarEventDto : CreateCalendarEventDto
{
}

public sealed class CalendarEventDto
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid EventTypeId { get; set; }
    public string EventTypeName { get; set; } = string.Empty;
    public string EventTypeCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool AppliesToStudents { get; set; }
    public bool AppliesToTeachers { get; set; }
    public bool AppliesToStaff { get; set; }
    public bool IsNonWorkingDay { get; set; }
    public string Color { get; set; } = "#5B8DEF";
    public List<Guid> ClassIds { get; set; } = [];
}

public sealed class WorkingDaysResponseDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Audience { get; set; } = string.Empty;
    public int TotalWorkingDays { get; set; }
    public IReadOnlyList<int> NonWorkingDays { get; set; } = [];
}

public sealed record CreateCalendarEventTypeResponse(string Message, Guid Id);
public sealed record CreateCalendarEventResponse(string Message, Guid Id);

public static class AcademicCalendarMappingExtensions
{
    public static CalendarEventTypeEntity ToEntity(this CreateCalendarEventTypeDto dto) => new()
    {
        Name = dto.Name.Trim(),
        Code = dto.Code.Trim().ToUpperInvariant(),
        Color = string.IsNullOrWhiteSpace(dto.Color) ? "#5B8DEF" : dto.Color.Trim(),
        IsNonWorkingDefault = dto.IsNonWorkingDefault,
        DisplayOrder = dto.DisplayOrder
    };

    public static CalendarEventTypeDto ToDto(this CalendarEventTypeEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Code = e.Code,
        Color = e.Color,
        IsNonWorkingDefault = e.IsNonWorkingDefault,
        DisplayOrder = e.DisplayOrder,
        IsActive = e.IsActive
    };

    public static CalendarWeekendSettingDto ToDto(this CalendarWeekendSettingEntity e) => new()
    {
        Id = e.Id,
        BranchId = e.BranchId,
        SundayOff = e.SundayOff,
        SaturdayOff = e.SaturdayOff,
        MondayOff = e.MondayOff,
        TuesdayOff = e.TuesdayOff,
        WednesdayOff = e.WednesdayOff,
        ThursdayOff = e.ThursdayOff,
        FridayOff = e.FridayOff
    };

    public static CalendarEventEntity ToEntity(this CreateCalendarEventDto dto) => new()
    {
        AcademicYearId = dto.AcademicYearId,
        EventTypeId = dto.EventTypeId,
        Title = dto.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        AppliesToStudents = dto.AppliesToStudents,
        AppliesToTeachers = dto.AppliesToTeachers,
        AppliesToStaff = dto.AppliesToStaff,
        IsNonWorkingDay = dto.IsNonWorkingDay,
        Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim()
    };
}
