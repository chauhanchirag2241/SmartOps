using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.AcademicCalendar;
using SmartOps.Application.Modules.AcademicCalendar.Interfaces;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Application.Modules.StaffAttendance;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.AcademicCalendar;
using SmartOps.Domain.Modules.AcademicCalendar.Entities;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.StaffAttendance;

namespace SmartOps.Infrastructure.Modules.AcademicCalendar.Services;

public sealed class AcademicCalendarService : IAcademicCalendarService
{
    private readonly IAcademicCalendarRepository _repo;
    private readonly IBranchContext _branchContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserScopeContext _scope;
    private readonly IExamRepository _examRepo;
    private readonly IStaffAttendanceRepository _attendanceRepo;
    private readonly ILeaveService _leaveService;

    public AcademicCalendarService(
        IAcademicCalendarRepository repo,
        IBranchContext branchContext,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IExamRepository examRepo,
        IStaffAttendanceRepository attendanceRepo,
        ILeaveService leaveService)
    {
        _repo = repo;
        _branchContext = branchContext;
        _currentUser = currentUser;
        _scope = scope;
        _examRepo = examRepo;
        _attendanceRepo = attendanceRepo;
        _leaveService = leaveService;
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
            ClassIds = classIds.ToList(),
            SourceExamId = entity.SourceExamId
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

    public async Task<Result<MyCalendarMonthDto>> GetMyMonthAsync(
        int year,
        int month,
        Guid? branchId = null,
        CancellationToken ct = default)
    {
        if (month is < 1 or > 12)
        {
            return Result<MyCalendarMonthDto>.Failure("Month must be between 1 and 12.");
        }

        if (year < 2000 || year > 2100)
        {
            return Result<MyCalendarMonthDto>.Failure("Year is out of range.");
        }

        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            return Result<MyCalendarMonthDto>.Failure("User is not authenticated.");
        }

        await _branchContext.EnsureResolvedAsync(ct).ConfigureAwait(false);

        Guid resolvedBranchId;
        if (branchId is Guid requested && requested != Guid.Empty && _branchContext.HasBranchAccess(requested))
        {
            resolvedBranchId = requested;
        }
        else if (_branchContext.ActiveBranchId is Guid active)
        {
            resolvedBranchId = active;
        }
        else if (branchId is Guid fallback && fallback != Guid.Empty)
        {
            // BranchContext had no mappings; still allow an explicit school branch from the client.
            resolvedBranchId = fallback;
        }
        else
        {
            return Result<MyCalendarMonthDto>.Failure("Active branch is required.");
        }

        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);

        DateOnly from = new(year, month, 1);
        DateOnly to = from.AddMonths(1).AddDays(-1);

        Guid? employeeId = await _attendanceRepo
            .GetEmployeeIdByUserIdAsync(_currentUser.UserId, ct)
            .ConfigureAwait(false);

        bool isStudent = _scope.OwnStudentId.HasValue;
        // Exams only for students (own class) and teachers (class rights / invigilator) — not admins/other staff.
        bool includeExams = isStudent
            || _scope.ScopeType is DataScopeType.Class or DataScopeType.SubjectClass;
        IReadOnlyList<Guid> allowedClasses = _scope.AllowedClassIds ?? [];
        bool isGlobal = !_scope.ScopesEnabled || _scope.IsGlobalScope;

        var items = new List<MyCalendarItemDto>();

        // ── Weekend offs (Academic Calendar weekend settings) ──
        CalendarWeekendSettingEntity? weekend = await _repo
            .GetWeekendSettingsAsync(resolvedBranchId, ct)
            .ConfigureAwait(false);
        for (DateOnly d = from; d <= to; d = d.AddDays(1))
        {
            if (!IsWeekendOff(weekend, d))
            {
                continue;
            }

            items.Add(new MyCalendarItemDto
            {
                Kind = "weekend",
                Id = Guid.Empty,
                Title = "Weekend / Day off",
                Description = $"{d.DayOfWeek} is configured as an off day for this branch.",
                StartDate = d,
                EndDate = d,
                Color = "#ECEFF1",
                EventTypeName = "Weekend",
                IsNonWorkingDay = true,
                StatusLabel = "Day off"
            });
        }

        // ── Holidays / events ──
        var events = await _repo
            .GetEventsForRangeAsync(resolvedBranchId, _scope.ActiveAcademicYearId, from, to, ct)
            .ConfigureAwait(false);

        foreach (CalendarEventDto ev in events)
        {
            // Exam-synced spanning events are shown as per-slot exam items below.
            if (ev.SourceExamId.HasValue && ev.SourceExamId.Value != Guid.Empty)
            {
                continue;
            }

            bool audienceOk = isStudent
                ? ev.AppliesToStudents
                : (ev.AppliesToTeachers || ev.AppliesToStaff);

            if (!audienceOk)
            {
                continue;
            }

            if (!isGlobal && !isStudent && ev.ClassIds is { Count: > 0 })
            {
                bool overlaps = ev.ClassIds.Any(c => allowedClasses.Contains(c));
                if (!overlaps)
                {
                    continue;
                }
            }

            if (isStudent && ev.ClassIds is { Count: > 0 })
            {
                bool overlaps = ev.ClassIds.Any(c => allowedClasses.Contains(c));
                if (!overlaps)
                {
                    continue;
                }
            }

            string kind = ev.IsNonWorkingDay ? "holiday" : "event";
            items.Add(new MyCalendarItemDto
            {
                Kind = kind,
                Id = ev.Id,
                Title = ev.Title,
                Description = ev.Description,
                StartDate = ev.StartDate,
                EndDate = ev.EndDate,
                Color = ev.Color,
                EventTypeName = ev.EventTypeName,
                IsNonWorkingDay = ev.IsNonWorkingDay,
                ClassNames = ev.ClassNames ?? [],
                StatusLabel = kind == "holiday" ? "Holiday" : "Event"
            });
        }

        // ── Exams (teacher / student only) ──
        if (includeExams)
        {
            IList<ExamScheduleRow> schedules = await _examRepo
                .GetSchedulesForMyCalendarAsync(
                    from,
                    to,
                    isStudent ? null : employeeId,
                    allowedClasses,
                    isGlobalScope: false,
                    ct)
                .ConfigureAwait(false);

            foreach (ExamScheduleRow slot in schedules)
            {
                items.Add(new MyCalendarItemDto
                {
                    Kind = "exam",
                    Id = slot.Id,
                    Title = string.IsNullOrWhiteSpace(slot.SubjectName)
                        ? slot.ExamName
                        : $"{slot.ExamName} · {slot.SubjectName}",
                    Description = null,
                    StartDate = slot.ExamDate,
                    EndDate = slot.ExamDate,
                    Color = "#FB8C00",
                    EventTypeName = "Exam",
                    IsNonWorkingDay = false,
                    ClassNames = string.IsNullOrWhiteSpace(slot.ClassName) ? [] : [slot.ClassName],
                    SubjectName = slot.SubjectName,
                    StartTime = slot.StartTime,
                    EndTime = slot.EndTime,
                    RoomNo = slot.RoomNo,
                    InvigilatorName = slot.InvigilatorName,
                    ExamName = slot.ExamName,
                    StatusLabel = "Exam"
                });
            }
        }

        // ── Own staff attendance + leave ──
        if (employeeId.HasValue)
        {
            var leaveDays = new HashSet<DateOnly>();
            Result<IList<LeaveListItemDto>> leaveResult = await _leaveService
                .GetStaffMineAsync(ct)
                .ConfigureAwait(false);
            if (leaveResult.IsSuccess && leaveResult.Value is not null)
            {
                foreach (LeaveListItemDto leave in leaveResult.Value)
                {
                    if (leave.Status is not (LeaveRequestStatus.Approved or LeaveRequestStatus.Submitted))
                    {
                        continue;
                    }

                    DateOnly leaveFrom = leave.FromDate < from ? from : leave.FromDate;
                    DateOnly leaveTo = leave.ToDate > to ? to : leave.ToDate;
                    for (DateOnly d = leaveFrom; d <= leaveTo; d = d.AddDays(1))
                    {
                        leaveDays.Add(d);
                        items.Add(new MyCalendarItemDto
                        {
                            Kind = "leave",
                            Id = leave.Id,
                            Title = leave.LeaveTypeName ?? leave.LeaveTypeLabel ?? "Leave",
                            Description = leave.StatusLabel,
                            StartDate = d,
                            EndDate = d,
                            Color = "#E3F2FD",
                            EventTypeName = "Leave",
                            IsNonWorkingDay = false,
                            StatusLabel = "Leave"
                        });
                    }
                }
            }

            IList<StaffAttendanceDayStatusRow> attendanceRows = await _attendanceRepo
                .GetEmployeeMonthStatusesAsync(employeeId.Value, month, year, ct)
                .ConfigureAwait(false);

            foreach (StaffAttendanceDayStatusRow row in attendanceRows)
            {
                if (leaveDays.Contains(row.AttendanceDate))
                {
                    continue;
                }

                string kind = row.Status switch
                {
                    StaffAttendanceStatus.Present => "present",
                    StaffAttendanceStatus.Absent => "absent",
                    StaffAttendanceStatus.Late => "late",
                    StaffAttendanceStatus.HalfDay => "halfday",
                    _ => "present"
                };

                items.Add(new MyCalendarItemDto
                {
                    Kind = kind,
                    Id = Guid.Empty,
                    Title = row.Status.ToDisplayString(),
                    Description = "Your attendance for this day.",
                    StartDate = row.AttendanceDate,
                    EndDate = row.AttendanceDate,
                    Color = kind switch
                    {
                        "present" => "#C8E6C9",
                        "absent" => "#FFCDD2",
                        "late" => "#FFE082",
                        "halfday" => "#B2DFDB",
                        _ => "#C8E6C9"
                    },
                    EventTypeName = "Attendance",
                    IsNonWorkingDay = false,
                    StatusLabel = row.Status.ToDisplayString()
                });
            }
        }

        items = items
            .OrderBy(i => i.StartDate)
            .ThenBy(i => KindSortOrder(i.Kind))
            .ThenBy(i => i.StartTime ?? string.Empty)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<MyCalendarMonthDto>.Success(new MyCalendarMonthDto
        {
            Year = year,
            Month = month,
            Items = items
        });
    }

    private static int KindSortOrder(string kind) => kind.ToLowerInvariant() switch
    {
        "leave" => 0,
        "present" or "late" or "halfday" or "absent" => 1,
        "exam" => 2,
        "holiday" => 3,
        "weekend" => 4,
        _ => 5
    };

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
