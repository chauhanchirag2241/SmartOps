using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Modules.Authorization.Services;

public sealed class ResourceAuthorizationService : IResourceAuthorizationService
{
    private readonly IUserScopeContext _scope;

    public ResourceAuthorizationService(IUserScopeContext scope)
    {
        _scope = scope;
    }

    public async Task<bool> CanAccessStudentAsync(
        Guid studentId,
        AccessLevel level,
        CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_scope.ScopeType == DataScopeType.ModuleOnly)
        {
            return false;
        }

        return _scope.HasStudentAccess(studentId);
    }

    public async Task<bool> CanAccessClassAsync(
        Guid classId,
        AccessLevel level,
        CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_scope.ScopeType == DataScopeType.ModuleOnly || _scope.ScopeType == DataScopeType.Self)
        {
            return false;
        }

        return _scope.HasClassAccess(classId);
    }

    public async Task<bool> CanMarkAttendanceForClassAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_scope.ScopeType == DataScopeType.ModuleOnly || _scope.ScopeType == DataScopeType.Self)
        {
            return false;
        }

        return _scope.HasAttendanceClassAccess(classId);
    }

    public async Task<bool> CanAccessTeacherAsync(
        Guid teacherId,
        AccessLevel level,
        CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_scope.ScopeType == DataScopeType.ModuleOnly
            || _scope.ScopeType == DataScopeType.Self
            || _scope.ScopeType == DataScopeType.LinkedStudents)
        {
            return false;
        }

        if (_scope.IsGlobalScope)
        {
            return true;
        }

        return _scope.HasTeacherAccess(teacherId);
    }
}
