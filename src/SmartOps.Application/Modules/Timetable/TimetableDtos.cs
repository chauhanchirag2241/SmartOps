namespace SmartOps.Application.Modules.Timetable;

public sealed class CreateTimetableVersionDto
{
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public Guid PeriodTemplateId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Notes { get; set; }
    public bool CopyFromPrevious { get; set; }
}

public sealed class UpsertTimetableSlotsDto
{
    public IReadOnlyList<TimetableSlotInputDto> Slots { get; set; } = [];
}

public sealed class TimetableSlotInputDto
{
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? RoomNo { get; set; }
}

public sealed class ValidateConflictsDto
{
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public Guid? TimetableId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public IReadOnlyList<TimetableSlotInputDto> Slots { get; set; } = [];
}

public sealed record CreateTimetableResponse(string Message, Guid TimetableId);

public sealed class TimetableVersionDto
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public Guid PeriodTemplateId { get; set; }
    public string? PeriodTemplateName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed class TimetableGridDto
{
    public TimetableVersionDto? Version { get; set; }
    public IReadOnlyList<PeriodGridRowDto> Periods { get; set; } = [];
    public IReadOnlyList<TimetableSlotCellDto> Slots { get; set; } = [];
    public IReadOnlyList<TimetableConflictDto> Conflicts { get; set; } = [];
}

public sealed class PeriodGridRowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int PeriodOrder { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsBreak { get; set; }
}

public sealed class TimetableSlotCellDto
{
    public Guid? Id { get; set; }
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? SubjectCode { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? RoomNo { get; set; }
    public Guid? ClassId { get; set; }
    public string? ClassName { get; set; }
    public bool HasTeacherConflict { get; set; }
    public bool HasRoomConflict { get; set; }
}

public sealed class TimetableConflictDto
{
    public string Type { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? RoomNo { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ConflictCheckResultDto
{
    public bool HasConflicts { get; set; }
    public IReadOnlyList<TimetableConflictDto> Conflicts { get; set; } = [];
}

public sealed class MyTimetableResponseDto
{
    public string Persona { get; set; } = "none";
    public Guid? EmployeeId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? ClassId { get; set; }
    public string? ClassName { get; set; }
    public TimetableGridDto Grid { get; set; } = new();
}
