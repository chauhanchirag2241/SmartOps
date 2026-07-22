using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Domain.Modules.Timetable;

public interface ITimetableRepository
{
    Task<ClassTimetableEntity?> GetTimetableByIdAsync(Guid id, CancellationToken cancellationToken, bool includeInactive = false);
    Task<IReadOnlyList<ClassTimetableEntity>> GetVersionsAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken);
    Task<ClassTimetableEntity?> GetCurrentVersionAsync(Guid classId, Guid academicYearId, DateOnly asOf, CancellationToken cancellationToken);
    Task<Guid> CreateTimetableAsync(ClassTimetableEntity entity, CancellationToken cancellationToken);
    Task UpdateTimetableAsync(ClassTimetableEntity entity, CancellationToken cancellationToken);
    Task DeleteTimetableAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassTimetableSlotEntity>> GetSlotsByTimetableIdAsync(Guid timetableId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TimetableSlotDetailRow>> GetSlotDetailsByTimetableIdAsync(Guid timetableId, CancellationToken cancellationToken);
    Task ReplaceSlotsAsync(Guid timetableId, IReadOnlyList<ClassTimetableSlotEntity> slots, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimetableConflictRow>> FindTeacherConflictsAsync(
        Guid academicYearId,
        Guid excludeTimetableId,
        DateOnly effectiveFrom,
        IReadOnlyList<TimetableSlotConflictKey> keys,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TimetableConflictRow>> FindRoomConflictsAsync(
        Guid academicYearId,
        Guid excludeTimetableId,
        DateOnly effectiveFrom,
        IReadOnlyList<TimetableSlotConflictKey> keys,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TimetableSlotDetailRow>> GetSlotsForTeacherAsync(
        Guid academicYearId,
        Guid employeeId,
        DateOnly asOf,
        CancellationToken cancellationToken);

    Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> GetStudentIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> GetStudentClassIdAsync(Guid studentId, Guid academicYearId, CancellationToken cancellationToken);
}

public sealed record TimetableSlotConflictKey(int DayOfWeek, Guid PeriodId, Guid? EmployeeId, string? RoomNo);

public sealed class TimetableConflictRow
{
    public Guid TimetableId { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? RoomNo { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
}

public sealed class TimetableSlotDetailRow
{
    public Guid SlotId { get; set; }
    public Guid TimetableId { get; set; }
    public Guid ClassId { get; set; }
    public string? ClassName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public string? PeriodShortName { get; set; }
    public int PeriodOrder { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsBreak { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? SubjectCode { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? RoomNo { get; set; }
}
