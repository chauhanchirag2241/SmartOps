using SmartOps.Application.Modules.Timetable;

namespace SmartOps.Application.Modules.Timetable.Interfaces;

public interface ITimetableService
{
    Task<IReadOnlyList<TimetableVersionDto>> GetVersionsAsync(Guid classId, Guid academicYearId, CancellationToken ct);
    Task<CreateTimetableResponse> CreateVersionAsync(CreateTimetableVersionDto request, CancellationToken ct);
    Task<TimetableGridDto> GetGridAsync(Guid timetableId, CancellationToken ct);
    Task<TimetableGridDto> GetClassGridAsOfAsync(Guid classId, Guid academicYearId, DateOnly asOf, CancellationToken ct);
    Task<TimetableGridDto> GetTeacherGridAsync(Guid employeeId, Guid academicYearId, DateOnly asOf, CancellationToken ct);
    Task SaveSlotsAsync(Guid timetableId, UpsertTimetableSlotsDto request, CancellationToken ct);
    Task<ConflictCheckResultDto> ValidateConflictsAsync(ValidateConflictsDto request, CancellationToken ct);
    Task DeleteVersionAsync(Guid timetableId, CancellationToken ct);
    Task<MyTimetableResponseDto> GetMyTimetableAsync(Guid academicYearId, DateOnly asOf, CancellationToken ct);
}
