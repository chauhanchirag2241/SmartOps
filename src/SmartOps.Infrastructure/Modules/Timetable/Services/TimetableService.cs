using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Timetable;
using SmartOps.Application.Modules.Timetable.Interfaces;
using SmartOps.Domain.Modules.Class;
using SmartOps.Domain.Modules.Timetable;
using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Infrastructure.Modules.Timetable.Services;

public sealed class TimetableService(
    ITimetableRepository timetableRepository,
    IPeriodTemplateRepository periodTemplateRepository,
    IClassRepository classRepository,
    ICurrentUserService currentUser) : ITimetableService
{
    private static string FormatClassName(SmartOps.Domain.Modules.Class.Entities.ClassEntity cls)
        => $"{cls.ClassName}-{cls.Section}";

    public async Task<IReadOnlyList<TimetableVersionDto>> GetVersionsAsync(Guid classId, Guid academicYearId, CancellationToken ct)
    {
        var versions = await timetableRepository.GetVersionsAsync(classId, academicYearId, ct).ConfigureAwait(false);
        string? className = null;
        var cls = await classRepository.GetClassByIdAsync(classId, ct).ConfigureAwait(false);
        if (cls is not null) className = FormatClassName(cls);

        var result = new List<TimetableVersionDto>();
        foreach (var v in versions)
        {
            string? templateName = null;
            var template = await periodTemplateRepository.GetTemplateByIdAsync(v.PeriodTemplateId, ct, includeInactive: true)
                .ConfigureAwait(false);
            if (template is not null) templateName = template.Name;

            result.Add(new TimetableVersionDto
            {
                Id = v.Id,
                AcademicYearId = v.AcademicYearId,
                ClassId = v.ClassId,
                ClassName = className,
                PeriodTemplateId = v.PeriodTemplateId,
                PeriodTemplateName = templateName,
                EffectiveFrom = v.EffectiveFrom,
                Notes = v.Notes,
                IsActive = v.IsActive,
            });
        }

        return result;
    }

    public async Task<CreateTimetableResponse> CreateVersionAsync(CreateTimetableVersionDto request, CancellationToken ct)
    {
        if (request.ClassId == Guid.Empty || request.AcademicYearId == Guid.Empty)
            throw new InvalidOperationException("Class and academic year are required.");
        if (request.PeriodTemplateId == Guid.Empty)
            throw new InvalidOperationException("Period template is required.");

        var template = await periodTemplateRepository.GetTemplateByIdAsync(request.PeriodTemplateId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Selected period template was not found.");

        var templatePeriods = await periodTemplateRepository.GetPeriodsByTemplateIdAsync(template.Id, ct)
            .ConfigureAwait(false);
        if (templatePeriods.Count == 0)
            throw new InvalidOperationException("Selected period template has no periods.");

        var existing = await timetableRepository.GetVersionsAsync(request.ClassId, request.AcademicYearId, ct)
            .ConfigureAwait(false);
        if (existing.Any(v => v.EffectiveFrom == request.EffectiveFrom))
            throw new InvalidOperationException("A timetable version with this effective-from date already exists.");

        var entity = new ClassTimetableEntity
        {
            AcademicYearId = request.AcademicYearId,
            ClassId = request.ClassId,
            PeriodTemplateId = request.PeriodTemplateId,
            EffectiveFrom = request.EffectiveFrom,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        var id = await timetableRepository.CreateTimetableAsync(entity, ct).ConfigureAwait(false);

        if (request.CopyFromPrevious)
        {
            var previous = existing
                .Where(v => v.EffectiveFrom < request.EffectiveFrom && v.PeriodTemplateId == request.PeriodTemplateId)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefault();
            if (previous is not null)
            {
                var allowedPeriodIds = templatePeriods.Select(p => p.Id).ToHashSet();
                var prevSlots = await timetableRepository.GetSlotsByTimetableIdAsync(previous.Id, ct).ConfigureAwait(false);
                var copies = prevSlots
                    .Where(s => allowedPeriodIds.Contains(s.PeriodId))
                    .Select(s => new ClassTimetableSlotEntity
                    {
                        DayOfWeek = s.DayOfWeek,
                        PeriodId = s.PeriodId,
                        SubjectId = s.SubjectId,
                        EmployeeId = s.EmployeeId,
                        RoomNo = s.RoomNo,
                    })
                    .ToList();
                if (copies.Count > 0)
                    await timetableRepository.ReplaceSlotsAsync(id, copies, ct).ConfigureAwait(false);
            }
        }

        return new CreateTimetableResponse("Timetable version created", id);
    }

    public async Task<TimetableGridDto> GetGridAsync(Guid timetableId, CancellationToken ct)
    {
        var version = await timetableRepository.GetTimetableByIdAsync(timetableId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Timetable version not found.");
        return await BuildClassGridAsync(version, ct).ConfigureAwait(false);
    }

    public async Task<TimetableGridDto> GetClassGridAsOfAsync(Guid classId, Guid academicYearId, DateOnly asOf, CancellationToken ct)
    {
        var version = await timetableRepository.GetCurrentVersionAsync(classId, academicYearId, asOf, ct)
            .ConfigureAwait(false);
        if (version is null)
            return new TimetableGridDto { Periods = [], Slots = [] };

        return await BuildClassGridAsync(version, ct).ConfigureAwait(false);
    }

    public async Task<TimetableGridDto> GetTeacherGridAsync(Guid employeeId, Guid academicYearId, DateOnly asOf, CancellationToken ct)
    {
        var details = await timetableRepository.GetSlotsForTeacherAsync(academicYearId, employeeId, asOf, ct)
            .ConfigureAwait(false);

        var periods = details
            .GroupBy(d => d.PeriodId)
            .Select(g => g.First())
            .OrderBy(d => d.PeriodOrder)
            .Select(d => new PeriodGridRowDto
            {
                Id = d.PeriodId,
                Name = d.PeriodName ?? "",
                ShortName = d.PeriodShortName ?? "",
                PeriodOrder = d.PeriodOrder,
                StartTime = d.StartTime ?? "",
                EndTime = d.EndTime ?? "",
                IsBreak = d.IsBreak,
            })
            .ToList();

        return new TimetableGridDto
        {
            Periods = periods,
            Slots = details.Select(d => new TimetableSlotCellDto
            {
                Id = d.SlotId,
                DayOfWeek = d.DayOfWeek,
                PeriodId = d.PeriodId,
                SubjectId = d.SubjectId,
                SubjectName = d.SubjectName,
                SubjectCode = d.SubjectCode,
                EmployeeId = d.EmployeeId,
                EmployeeName = d.EmployeeName,
                RoomNo = d.RoomNo,
                ClassId = d.ClassId,
                ClassName = d.ClassName,
            }).ToList(),
        };
    }

    public async Task SaveSlotsAsync(Guid timetableId, UpsertTimetableSlotsDto request, CancellationToken ct)
    {
        var version = await timetableRepository.GetTimetableByIdAsync(timetableId, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Timetable version not found.");

        var periods = (await periodTemplateRepository.GetPeriodsByTemplateIdAsync(version.PeriodTemplateId, ct)
            .ConfigureAwait(false)).ToDictionary(p => p.Id);

        var cleaned = new List<ClassTimetableSlotEntity>();
        foreach (var slot in request.Slots ?? [])
        {
            if (slot.DayOfWeek < 1 || slot.DayOfWeek > 6)
                throw new InvalidOperationException("Day of week must be Monday(1)–Saturday(6).");

            if (!periods.TryGetValue(slot.PeriodId, out var period))
                throw new InvalidOperationException("Period does not belong to this timetable's template.");

            if (period.IsBreak) continue;

            if (!slot.SubjectId.HasValue && !slot.EmployeeId.HasValue && string.IsNullOrWhiteSpace(slot.RoomNo))
                continue;

            if (!slot.SubjectId.HasValue)
                throw new InvalidOperationException("Subject is required for teaching periods.");

            // Subject/teacher may be assigned directly on the timetable — Class Mapping is optional.
            cleaned.Add(new ClassTimetableSlotEntity
            {
                DayOfWeek = slot.DayOfWeek,
                PeriodId = slot.PeriodId,
                SubjectId = slot.SubjectId,
                EmployeeId = slot.EmployeeId,
                RoomNo = string.IsNullOrWhiteSpace(slot.RoomNo) ? null : slot.RoomNo.Trim(),
            });
        }

        var conflictResult = await ValidateConflictsAsync(new ValidateConflictsDto
        {
            AcademicYearId = version.AcademicYearId,
            ClassId = version.ClassId,
            TimetableId = timetableId,
            EffectiveFrom = version.EffectiveFrom,
            Slots = cleaned.Select(s => new TimetableSlotInputDto
            {
                DayOfWeek = s.DayOfWeek,
                PeriodId = s.PeriodId,
                SubjectId = s.SubjectId,
                EmployeeId = s.EmployeeId,
                RoomNo = s.RoomNo,
            }).ToList(),
        }, ct).ConfigureAwait(false);

        if (conflictResult.HasConflicts)
        {
            var msg = string.Join("; ", conflictResult.Conflicts.Select(c => c.Message).Take(3));
            throw new InvalidOperationException($"Conflicts detected: {msg}");
        }

        await timetableRepository.ReplaceSlotsAsync(timetableId, cleaned, ct).ConfigureAwait(false);
    }

    public async Task<ConflictCheckResultDto> ValidateConflictsAsync(ValidateConflictsDto request, CancellationToken ct)
    {
        var keys = (request.Slots ?? [])
            .Where(s => s.EmployeeId.HasValue || !string.IsNullOrWhiteSpace(s.RoomNo))
            .Select(s => new TimetableSlotConflictKey(
                s.DayOfWeek,
                s.PeriodId,
                s.EmployeeId,
                string.IsNullOrWhiteSpace(s.RoomNo) ? null : s.RoomNo.Trim()))
            .ToList();

        var excludeId = request.TimetableId ?? Guid.Empty;
        var teacherRows = await timetableRepository.FindTeacherConflictsAsync(
            request.AcademicYearId, excludeId, request.EffectiveFrom, keys, ct).ConfigureAwait(false);
        var roomRows = await timetableRepository.FindRoomConflictsAsync(
            request.AcademicYearId, excludeId, request.EffectiveFrom, keys, ct).ConfigureAwait(false);

        var conflicts = new List<TimetableConflictDto>();
        foreach (var row in teacherRows)
        {
            conflicts.Add(new TimetableConflictDto
            {
                Type = "teacher",
                DayOfWeek = row.DayOfWeek,
                PeriodId = row.PeriodId,
                PeriodName = row.PeriodName,
                EmployeeId = row.EmployeeId,
                EmployeeName = row.EmployeeName,
                ClassId = row.ClassId,
                ClassName = row.ClassName,
                EffectiveFrom = row.EffectiveFrom,
                Message = $"Teacher {row.EmployeeName} already assigned to {row.ClassName} (period {row.PeriodName}, day {row.DayOfWeek}).",
            });
        }

        foreach (var row in roomRows)
        {
            conflicts.Add(new TimetableConflictDto
            {
                Type = "room",
                DayOfWeek = row.DayOfWeek,
                PeriodId = row.PeriodId,
                PeriodName = row.PeriodName,
                RoomNo = row.RoomNo,
                ClassId = row.ClassId,
                ClassName = row.ClassName,
                EffectiveFrom = row.EffectiveFrom,
                Message = $"Room {row.RoomNo} already used by {row.ClassName} (period {row.PeriodName}, day {row.DayOfWeek}).",
            });
        }

        return new ConflictCheckResultDto
        {
            HasConflicts = conflicts.Count > 0,
            Conflicts = conflicts,
        };
    }

    public async Task DeleteVersionAsync(Guid timetableId, CancellationToken ct)
    {
        await timetableRepository.DeleteTimetableAsync(timetableId, ct).ConfigureAwait(false);
    }

    public async Task<MyTimetableResponseDto> GetMyTimetableAsync(Guid academicYearId, DateOnly asOf, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var employeeId = await timetableRepository.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (employeeId.HasValue)
        {
            return new MyTimetableResponseDto
            {
                Persona = "teacher",
                EmployeeId = employeeId,
                Grid = await GetTeacherGridAsync(employeeId.Value, academicYearId, asOf, ct).ConfigureAwait(false),
            };
        }

        var studentId = await timetableRepository.GetStudentIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (studentId.HasValue)
        {
            var classId = await timetableRepository.GetStudentClassIdAsync(studentId.Value, academicYearId, ct)
                .ConfigureAwait(false);
            if (classId.HasValue)
            {
                var grid = await GetClassGridAsOfAsync(classId.Value, academicYearId, asOf, ct).ConfigureAwait(false);
                string? className = null;
                var cls = await classRepository.GetClassByIdAsync(classId.Value, ct).ConfigureAwait(false);
                if (cls is not null) className = FormatClassName(cls);

                return new MyTimetableResponseDto
                {
                    Persona = "student",
                    StudentId = studentId,
                    ClassId = classId,
                    ClassName = className,
                    Grid = grid,
                };
            }
        }

        return new MyTimetableResponseDto { Persona = "none" };
    }

    private async Task<TimetableGridDto> BuildClassGridAsync(ClassTimetableEntity version, CancellationToken ct)
    {
        var periods = await periodTemplateRepository.GetPeriodsByTemplateIdAsync(version.PeriodTemplateId, ct)
            .ConfigureAwait(false);

        IReadOnlyList<TimetableSlotDetailRow> slots;
        try
        {
            slots = await timetableRepository.GetSlotDetailsByTimetableIdAsync(version.Id, ct).ConfigureAwait(false);
        }
        catch
        {
            // Fallback if name-join query fails — still load the grid structure.
            var raw = await timetableRepository.GetSlotsByTimetableIdAsync(version.Id, ct).ConfigureAwait(false);
            slots = raw.Select(s => new TimetableSlotDetailRow
            {
                SlotId = s.Id,
                TimetableId = s.TimetableId,
                DayOfWeek = s.DayOfWeek,
                PeriodId = s.PeriodId,
                SubjectId = s.SubjectId,
                EmployeeId = s.EmployeeId,
                RoomNo = s.RoomNo,
            }).ToList();
        }

        string? className = null;
        var cls = await classRepository.GetClassByIdAsync(version.ClassId, ct).ConfigureAwait(false);
        if (cls is not null) className = FormatClassName(cls);

        string? templateName = null;
        var template = await periodTemplateRepository.GetTemplateByIdAsync(version.PeriodTemplateId, ct, includeInactive: true)
            .ConfigureAwait(false);
        if (template is not null) templateName = template.Name;

        var conflictCheck = await ValidateConflictsAsync(new ValidateConflictsDto
        {
            AcademicYearId = version.AcademicYearId,
            ClassId = version.ClassId,
            TimetableId = version.Id,
            EffectiveFrom = version.EffectiveFrom,
            Slots = slots.Select(s => new TimetableSlotInputDto
            {
                DayOfWeek = s.DayOfWeek,
                PeriodId = s.PeriodId,
                SubjectId = s.SubjectId,
                EmployeeId = s.EmployeeId,
                RoomNo = s.RoomNo,
            }).ToList(),
        }, ct).ConfigureAwait(false);

        var teacherConflictKeys = conflictCheck.Conflicts
            .Where(c => c.Type == "teacher")
            .Select(c => (c.DayOfWeek, c.PeriodId))
            .ToHashSet();
        var roomConflictKeys = conflictCheck.Conflicts
            .Where(c => c.Type == "room")
            .Select(c => (c.DayOfWeek, c.PeriodId))
            .ToHashSet();

        return new TimetableGridDto
        {
            Version = new TimetableVersionDto
            {
                Id = version.Id,
                AcademicYearId = version.AcademicYearId,
                ClassId = version.ClassId,
                ClassName = className,
                PeriodTemplateId = version.PeriodTemplateId,
                PeriodTemplateName = templateName,
                EffectiveFrom = version.EffectiveFrom,
                Notes = version.Notes,
                IsActive = version.IsActive,
            },
            Periods = periods.Select(ToPeriodDto).ToList(),
            Slots = slots.Select(s => new TimetableSlotCellDto
            {
                Id = s.SlotId,
                DayOfWeek = s.DayOfWeek,
                PeriodId = s.PeriodId,
                SubjectId = s.SubjectId,
                SubjectName = s.SubjectName,
                SubjectCode = s.SubjectCode,
                EmployeeId = s.EmployeeId,
                EmployeeName = s.EmployeeName,
                RoomNo = s.RoomNo,
                ClassId = version.ClassId,
                ClassName = className ?? s.ClassName,
                HasTeacherConflict = teacherConflictKeys.Contains((s.DayOfWeek, s.PeriodId)),
                HasRoomConflict = roomConflictKeys.Contains((s.DayOfWeek, s.PeriodId)),
            }).ToList(),
            Conflicts = conflictCheck.Conflicts,
        };
    }

    private static PeriodGridRowDto ToPeriodDto(PeriodEntity p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        ShortName = p.ShortName,
        PeriodOrder = p.PeriodOrder,
        StartTime = p.StartTime,
        EndTime = p.EndTime,
        IsBreak = p.IsBreak,
    };
}
