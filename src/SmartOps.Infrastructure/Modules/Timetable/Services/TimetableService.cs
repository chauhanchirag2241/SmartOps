using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Application.Modules.Timetable;
using SmartOps.Application.Modules.Timetable.Interfaces;
using SmartOps.Domain.Modules.Class;
using SmartOps.Domain.Modules.Timetable;
using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Infrastructure.Modules.Timetable.Services;

public sealed class TimetableService(
    ITimetableRepository timetableRepository,
    IPeriodRepository periodRepository,
    IClassSubjectTeacherMappingRepository mappingRepository,
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
        if (cls is not null)
        {
            className = FormatClassName(cls);
        }

        return versions.Select(v => new TimetableVersionDto
        {
            Id = v.Id,
            AcademicYearId = v.AcademicYearId,
            ClassId = v.ClassId,
            ClassName = className,
            EffectiveFrom = v.EffectiveFrom,
            Notes = v.Notes,
            IsActive = v.IsActive,
        }).ToList();
    }

    public async Task<CreateTimetableResponse> CreateVersionAsync(CreateTimetableVersionDto request, CancellationToken ct)
    {
        if (request.ClassId == Guid.Empty || request.AcademicYearId == Guid.Empty)
        {
            throw new InvalidOperationException("Class and academic year are required.");
        }

        var existing = await timetableRepository.GetVersionsAsync(request.ClassId, request.AcademicYearId, ct)
            .ConfigureAwait(false);
        if (existing.Any(v => v.EffectiveFrom == request.EffectiveFrom))
        {
            throw new InvalidOperationException("A timetable version with this effective-from date already exists.");
        }

        var entity = new ClassTimetableEntity
        {
            AcademicYearId = request.AcademicYearId,
            ClassId = request.ClassId,
            EffectiveFrom = request.EffectiveFrom,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        var id = await timetableRepository.CreateTimetableAsync(entity, ct).ConfigureAwait(false);

        if (request.CopyFromPrevious)
        {
            var previous = existing
                .Where(v => v.EffectiveFrom < request.EffectiveFrom)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefault();
            if (previous is not null)
            {
                var prevSlots = await timetableRepository.GetSlotsByTimetableIdAsync(previous.Id, ct).ConfigureAwait(false);
                var copies = prevSlots.Select(s => new ClassTimetableSlotEntity
                {
                    DayOfWeek = s.DayOfWeek,
                    PeriodId = s.PeriodId,
                    SubjectId = s.SubjectId,
                    EmployeeId = s.EmployeeId,
                    RoomNo = s.RoomNo,
                }).ToList();
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
        {
            var periods = await periodRepository.GetActivePeriodsOrderedAsync(ct).ConfigureAwait(false);
            return new TimetableGridDto
            {
                Periods = periods.Select(ToPeriodDto).ToList(),
                Slots = [],
            };
        }

        return await BuildClassGridAsync(version, ct).ConfigureAwait(false);
    }

    public async Task<TimetableGridDto> GetTeacherGridAsync(Guid employeeId, Guid academicYearId, DateOnly asOf, CancellationToken ct)
    {
        var periods = await periodRepository.GetActivePeriodsOrderedAsync(ct).ConfigureAwait(false);
        var details = await timetableRepository.GetSlotsForTeacherAsync(academicYearId, employeeId, asOf, ct)
            .ConfigureAwait(false);

        return new TimetableGridDto
        {
            Periods = periods.Select(ToPeriodDto).ToList(),
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

        var periods = (await periodRepository.GetActivePeriodsOrderedAsync(ct).ConfigureAwait(false))
            .ToDictionary(p => p.Id);
        var mappings = await mappingRepository.GetByClassIdAsync(version.ClassId, version.AcademicYearId, ct)
            .ConfigureAwait(false);

        var cleaned = new List<ClassTimetableSlotEntity>();
        foreach (var slot in request.Slots ?? [])
        {
            if (slot.DayOfWeek < 1 || slot.DayOfWeek > 6)
            {
                throw new InvalidOperationException("Day of week must be Monday(1)–Saturday(6).");
            }

            if (!periods.TryGetValue(slot.PeriodId, out var period))
            {
                throw new InvalidOperationException("Unknown period in timetable slots.");
            }

            if (period.IsBreak)
            {
                continue;
            }

            if (!slot.SubjectId.HasValue && !slot.EmployeeId.HasValue && string.IsNullOrWhiteSpace(slot.RoomNo))
            {
                continue;
            }

            if (!slot.SubjectId.HasValue)
            {
                throw new InvalidOperationException("Subject is required for teaching periods.");
            }

            var subjectMapped = mappings.Any(m => m.SubjectId == slot.SubjectId.Value);
            if (!subjectMapped)
            {
                throw new InvalidOperationException("Subject is not mapped to this class. Use Class Mapping first.");
            }

            if (slot.EmployeeId.HasValue)
            {
                var ok = mappings.Any(m => m.SubjectId == slot.SubjectId.Value && m.EmployeeId == slot.EmployeeId);
                if (!ok)
                {
                    throw new InvalidOperationException("Teacher is not mapped to this subject for the class.");
                }
            }

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
            var grid = await GetTeacherGridAsync(employeeId.Value, academicYearId, asOf, ct).ConfigureAwait(false);
            return new MyTimetableResponseDto
            {
                Persona = "teacher",
                EmployeeId = employeeId,
                Grid = grid,
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
                if (cls is not null)
                {
                    className = FormatClassName(cls);
                }

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
        var periods = await periodRepository.GetActivePeriodsOrderedAsync(ct).ConfigureAwait(false);
        var slots = await timetableRepository.GetSlotsByTimetableIdAsync(version.Id, ct).ConfigureAwait(false);

        string? className = null;
        var cls = await classRepository.GetClassByIdAsync(version.ClassId, ct).ConfigureAwait(false);
        if (cls is not null)
        {
            className = FormatClassName(cls);
        }

        // Enrich subject/teacher names via mappings.
        var mappings = await mappingRepository.GetByClassIdAsync(version.ClassId, version.AcademicYearId, ct)
            .ConfigureAwait(false);
        var subjectNames = mappings
            .GroupBy(m => m.SubjectId)
            .ToDictionary(g => g.Key, g => (g.First().SubjectName, g.First().SubjectCode));
        var employeeNames = mappings
            .Where(m => m.EmployeeId.HasValue)
            .GroupBy(m => m.EmployeeId!.Value)
            .ToDictionary(g => g.Key, g => g.First().EmployeeName);

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
                EffectiveFrom = version.EffectiveFrom,
                Notes = version.Notes,
                IsActive = version.IsActive,
            },
            Periods = periods.Select(ToPeriodDto).ToList(),
            Slots = slots.Select(s =>
            {
                string? subjectName = null;
                string? subjectCode = null;
                if (s.SubjectId.HasValue && subjectNames.TryGetValue(s.SubjectId.Value, out var sn))
                {
                    subjectName = sn.SubjectName;
                    subjectCode = sn.SubjectCode;
                }

                string? employeeName = null;
                if (s.EmployeeId.HasValue && employeeNames.TryGetValue(s.EmployeeId.Value, out var en))
                {
                    employeeName = en;
                }

                return new TimetableSlotCellDto
                {
                    Id = s.Id,
                    DayOfWeek = s.DayOfWeek,
                    PeriodId = s.PeriodId,
                    SubjectId = s.SubjectId,
                    SubjectName = subjectName,
                    SubjectCode = subjectCode,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = employeeName,
                    RoomNo = s.RoomNo,
                    ClassId = version.ClassId,
                    ClassName = className,
                    HasTeacherConflict = teacherConflictKeys.Contains((s.DayOfWeek, s.PeriodId)),
                    HasRoomConflict = roomConflictKeys.Contains((s.DayOfWeek, s.PeriodId)),
                };
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
