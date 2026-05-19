namespace SmartOps.Application.Modules.Authorization.Interfaces;

public enum AccessLevel
{
    View,
    Edit,
    Delete
}

public interface IResourceAuthorizationService
{
    Task<bool> CanAccessStudentAsync(Guid studentId, AccessLevel level, CancellationToken cancellationToken = default);

    Task<bool> CanAccessClassAsync(Guid classId, AccessLevel level, CancellationToken cancellationToken = default);

    Task<bool> CanMarkAttendanceForClassAsync(Guid classId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessTeacherAsync(Guid teacherId, AccessLevel level, CancellationToken cancellationToken = default);
}
