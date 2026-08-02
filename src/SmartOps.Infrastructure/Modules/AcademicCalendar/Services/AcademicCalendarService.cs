using SmartOps.Application.Modules.AcademicCalendar;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.AcademicCalendar.Entities;

namespace SmartOps.Infrastructure.Modules.AcademicCalendar.Services;

public sealed class AcademicCalendarService : IAcademicCalendarService
{
    private readonly IAcademicCalendarRepository _repo;
    private readonly IBranchContext _branchContext;

    public AcademicCalendarService(IAcademicCalendarRepository repo, IBranchContext branchContext)
    {
        _repo = repo;
        _branchContext = branchContext;
    }

    public async Task<Result<IReadOnlyList<CalendarEventTypeDto>>> GetEventTypesAsync(CancellationToken ct = default)
    {
        var types = await _repo.GetEventTypesAsync(ct).ConfigureAwait(false);
        return Result<IReadOnlyList<CalendarEventTypeDto>>.Success(types.Select(t => t.ToDto()).ToList());
    }

    public async Task<Result<CreateCalendarEventTypeResponse>> CreateEventTypeAsync(
        CreateCalendarEventTypeDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result<CreateCalendarEventTypeResponse>.Failure("Event type name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return Result<CreateCalendarEventTypeResponse>.Failure("Event type code is required.");
        }

        if (await _repo.EventTypeCodeExistsAsync(dto.Code.Trim(), null, ct).ConfigureAwait(false))
        {
            return Result<CreateCalendarEventTypeResponse>.Failure("Event type code already exists.");
        }

        var entity = dto.ToEntity();
        var id = await _repo.CreateEventTypeAsync(entity, ct).ConfigureAwait(false);
        return Result<CreateCalendarEventTypeResponse>.Success(
            new CreateCalendarEventTypeResponse("Event type created successfully", id));
    }

    public async Task<Result> UpdateEventTypeAsync(Guid id, UpdateCalendarEventTypeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return Result.Failure("Event type name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return Result.Failure("Event type code is required.");
        }

        var existing = await _repo.GetEventTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("Event type not found.");
        }

        if (await _repo.EventTypeCodeExistsAsync(dto.Code.Trim(), id, ct).ConfigureAwait(false))
        {
            return Result.Failure("Event type code already exists.");
        }

        existing.Name = dto.Name.Trim();
        existing.Code = dto.Code.Trim().ToUpperInvariant();
        existing.Color = string.IsNullOrWhiteSpace(dto.Color) ? "#5B8DEF" : dto.Color.Trim();
        existing.IsNonWorkingDefault = dto.IsNonWorkingDefault;
        existing.DisplayOrder = dto.DisplayOrder;

        await _repo.UpdateEventTypeAsync(existing, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> DeleteEventTypeAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _repo.GetEventTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("Event type not found.");
        }

        if (await _repo.EventTypeInUseAsync(id, ct).ConfigureAwait(false))
        {
            return Result.Failure("Event type is in use by calendar events and cannot be deleted.");
        }

        await _repo.DeleteEventTypeAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<CalendarWeekendSettingDto>> GetWeekendSettingsAsync(CancellationToken ct = default)
    {
        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        if (_branchContext.ActiveBranchId is not Guid branchId)
        {
            return Result<CalendarWeekendSettingDto>.Failure("Active branch is required.");
        }

        var settings = await _repo.GetWeekendSettingsAsync(branchId, ct).ConfigureAwait(false);
        if (settings is null)
        {
            return Result<CalendarWeekendSettingDto>.Success(new CalendarWeekendSettingDto
            {
                Id = Guid.Empty,
                BranchId = branchId,
                SundayOff = true
            });
        }

        return Result<CalendarWeekendSettingDto>.Success(settings.ToDto());
    }

    public async Task<Result<CalendarWeekendSettingDto>> UpsertWeekendSettingsAsync(
        UpsertCalendarWeekendSettingDto dto,
        CancellationToken ct = default)
    {
        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        if (_branchContext.ActiveBranchId is not Guid branchId)
        {
            return Result<CalendarWeekendSettingDto>.Failure("Active branch is required.");
        }

        var entity = new CalendarWeekendSettingEntity
        {
            BranchId = branchId,
            SundayOff = dto.SundayOff,
            SaturdayOff = dto.SaturdayOff,
            MondayOff = dto.MondayOff,
            TuesdayOff = dto.TuesdayOff,
            WednesdayOff = dto.WednesdayOff,
            ThursdayOff = dto.ThursdayOff,
            FridayOff = dto.FridayOff
        };

        await _repo.UpsertWeekendSettingsAsync(entity, ct).ConfigureAwait(false);
        var saved = await _repo.GetWeekendSettingsAsync(branchId, ct).ConfigureAwait(false);
        return Result<CalendarWeekendSettingDto>.Success(
            saved?.ToDto() ?? entity.ToDto());
    }

    public async Task<Result<IReadOnlyList<CalendarEventDto>>> GetEventsForRangeAsync(
        Guid? academicYearId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        if (to < from)
        {
            return Result<IReadOnlyList<CalendarEventDto>>.Failure("End date must be on or after start date.");
        }

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        if (_branchContext.ActiveBranchId is not Guid branchId)
        {
            return Result<IReadOnlyList<CalendarEventDto>>.Failure("Active branch is required.");
        }

        var events = await _repo.GetEventsForRangeAsync(branchId, academicYearId, from, to, ct).ConfigureAwait(false);
        return Result<IReadOnlyList<CalendarEventDto>>.Success(events);
    }

    public async Task<Result<CalendarEventDto>> GetEventByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetEventByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<CalendarEventDto>.Failure("Calendar event not found.");
        }

        var type = await _repo.GetEventTypeByIdAsync(entity.EventTypeId, ct, includeInactive: true).ConfigureAwait(false);
        var classIds = await _repo.GetEventClassIdsAsync(entity.Id, ct).ConfigureAwait(false);
        return Result<CalendarEventDto>.Success(new CalendarEventDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            AcademicYearId = entity.AcademicYearId,
            EventTypeId = entity.EventTypeId,
            EventTypeName = type?.Name ?? string.Empty,
            EventTypeCode = type?.Code ?? string.Empty,
            Title = entity.Title,
            Description = entity.Description,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            AppliesToStudents = entity.AppliesToStudents,
            AppliesToTeachers = entity.AppliesToTeachers,
            AppliesToStaff = entity.AppliesToStaff,
            IsNonWorkingDay = entity.IsNonWorkingDay,
            Color = !string.IsNullOrWhiteSpace(entity.Color) ? entity.Color! : (type?.Color ?? "#5B8DEF"),
            ClassIds = classIds.ToList()
        });
    }

    public async Task<Result<CreateCalendarEventResponse>> CreateEventAsync(
        CreateCalendarEventDto dto,
        CancellationToken ct = default)
    {
        NormalizeClassAudience(dto);
        var validation = await ValidateEventDtoAsync(dto, ct).ConfigureAwait(false);
        if (!validation.IsSuccess)
        {
            return Result<CreateCalendarEventResponse>.Failure(validation.Error ?? "Invalid event.");
        }

        var entity = dto.ToEntity();
        var id = await _repo.CreateEventAsync(entity, dto.ClassIds ?? [], ct).ConfigureAwait(false);
        return Result<CreateCalendarEventResponse>.Success(
            new CreateCalendarEventResponse("Calendar event created successfully", id));
    }

    public async Task<Result> UpdateEventAsync(Guid id, UpdateCalendarEventDto dto, CancellationToken ct = default)
    {
        NormalizeClassAudience(dto);
        var validation = await ValidateEventDtoAsync(dto, ct).ConfigureAwait(false);
        if (!validation.IsSuccess)
        {
            return Result.Failure(validation.Error ?? "Invalid event.");
        }

        var existing = await _repo.GetEventByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("Calendar event not found.");
        }

        existing.AcademicYearId = dto.AcademicYearId;
        existing.EventTypeId = dto.EventTypeId;
        existing.Title = dto.Title.Trim();
        existing.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;
        existing.AppliesToStudents = dto.AppliesToStudents;
        existing.AppliesToTeachers = dto.AppliesToTeachers;
        existing.AppliesToStaff = dto.AppliesToStaff;
        existing.IsNonWorkingDay = dto.IsNonWorkingDay;
        existing.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();

        await _repo.UpdateEventAsync(existing, dto.ClassIds ?? [], ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _repo.GetEventByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("Calendar event not found.");
        }

        await _repo.DeleteEventAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<bool> IsWorkingDayAsync(
        Guid? branchId,
        DateOnly date,
        CalendarAudience audience,
        CancellationToken ct = default,
        Guid? classId = null)
    {
        var resolvedBranchId = await ResolveBranchIdAsync(branchId, ct).ConfigureAwait(false);
        if (resolvedBranchId is null)
        {
            return date.DayOfWeek != DayOfWeek.Sunday;
        }

        if (await IsWeekendOffAsync(resolvedBranchId.Value, date, ct).ConfigureAwait(false))
        {
            return false;
        }

        var nonWorking = await _repo
            .GetNonWorkingEventDatesAsync(resolvedBranchId.Value, date, date, audience, classId, ct)
            .ConfigureAwait(false);
        return nonWorking.Count == 0;
    }

    public async Task<int> CountWorkingDaysAsync(
        Guid? branchId,
        int year,
        int month,
        CalendarAudience audience,
        CancellationToken ct = default,
        Guid? classId = null)
    {
        var nonWorking = await GetNonWorkingDayNumbersAsync(branchId, year, month, audience, ct, classId).ConfigureAwait(false);
        int daysInMonth = DateTime.DaysInMonth(year, month);
        return Math.Max(0, daysInMonth - nonWorking.Count);
    }

    public async Task<IReadOnlySet<int>> GetNonWorkingDayNumbersAsync(
        Guid? branchId,
        int year,
        int month,
        CalendarAudience audience,
        CancellationToken ct = default,
        Guid? classId = null)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, daysInMonth);
        var nonWorking = new HashSet<int>();

        var resolvedBranchId = await ResolveBranchIdAsync(branchId, ct).ConfigureAwait(false);
        CalendarWeekendSettingEntity? weekend = null;
        if (resolvedBranchId is Guid bid)
        {
            weekend = await _repo.GetWeekendSettingsAsync(bid, ct).ConfigureAwait(false);
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            if (IsWeekendOff(weekend, date))
            {
                nonWorking.Add(day);
            }
        }

        if (resolvedBranchId is Guid branch)
        {
            var eventDates = await _repo
                .GetNonWorkingEventDatesAsync(branch, from, to, audience, classId, ct)
                .ConfigureAwait(false);
            foreach (var d in eventDates)
            {
                nonWorking.Add(d.Day);
            }
        }

        return nonWorking;
    }

    public async Task<Result<WorkingDaysResponseDto>> GetWorkingDaysAsync(
        int year,
        int month,
        CalendarAudience audience,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result<WorkingDaysResponseDto>.Failure("Month must be between 1 and 12.");
        }

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        var nonWorking = await GetNonWorkingDayNumbersAsync(_branchContext.ActiveBranchId, year, month, audience, ct)
            .ConfigureAwait(false);
        int total = await CountWorkingDaysAsync(_branchContext.ActiveBranchId, year, month, audience, ct)
            .ConfigureAwait(false);

        return Result<WorkingDaysResponseDto>.Success(new WorkingDaysResponseDto
        {
            Year = year,
            Month = month,
            Audience = audience.ToString(),
            TotalWorkingDays = total,
            NonWorkingDays = nonWorking.OrderBy(d => d).ToList()
        });
    }

    private async Task<Result> ValidateEventDtoAsync(CreateCalendarEventDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return Result.Failure("Event title is required.");
        }

        if (dto.AcademicYearId == Guid.Empty)
        {
            return Result.Failure("Academic year is required.");
        }

        if (dto.EventTypeId == Guid.Empty)
        {
            return Result.Failure("Event type is required.");
        }

        if (dto.EndDate < dto.StartDate)
        {
            return Result.Failure("End date must be on or after start date.");
        }

        if (!dto.AppliesToStudents && !dto.AppliesToTeachers && !dto.AppliesToStaff)
        {
            return Result.Failure("Select at least one audience (students, teachers, or staff).");
        }

        if (dto.ClassIds is { Count: > 0 } && !dto.AppliesToStudents)
        {
            return Result.Failure("Class targeting requires students audience.");
        }

        var type = await _repo.GetEventTypeByIdAsync(dto.EventTypeId, ct).ConfigureAwait(false);
        if (type is null)
        {
            return Result.Failure("Event type not found.");
        }

        return Result.Success();
    }

    private static void NormalizeClassAudience(CreateCalendarEventDto dto)
    {
        dto.ClassIds = (dto.ClassIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (dto.ClassIds.Count > 0)
        {
            dto.AppliesToStudents = true;
        }
    }

    private async Task<Guid?> ResolveBranchIdAsync(Guid? branchId, CancellationToken ct)
    {
        if (branchId is Guid id && id != Guid.Empty)
        {
            return id;
        }

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);
        return _branchContext.ActiveBranchId;
    }

    private async Task<bool> IsWeekendOffAsync(Guid branchId, DateOnly date, CancellationToken ct)
    {
        var weekend = await _repo.GetWeekendSettingsAsync(branchId, ct).ConfigureAwait(false);
        return IsWeekendOff(weekend, date);
    }

    private static bool IsWeekendOff(CalendarWeekendSettingEntity? weekend, DateOnly date)
    {
        // Default: Sunday off when no settings row exists (backward compatible).
        if (weekend is null)
        {
            return date.DayOfWeek == DayOfWeek.Sunday;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Sunday => weekend.SundayOff,
            DayOfWeek.Monday => weekend.MondayOff,
            DayOfWeek.Tuesday => weekend.TuesdayOff,
            DayOfWeek.Wednesday => weekend.WednesdayOff,
            DayOfWeek.Thursday => weekend.ThursdayOff,
            DayOfWeek.Friday => weekend.FridayOff,
            DayOfWeek.Saturday => weekend.SaturdayOff,
            _ => false
        };
    }
}
