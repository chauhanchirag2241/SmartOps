using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Domain.Common.Enums;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.Modules.Authorization.Context;

public sealed class UserScopeContext : IUserScopeContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserScopeService _scopeService;
    private readonly TenantContext _tenantContext;
    private readonly AuthorizationOptions _options;
    private UserScopeDto? _scope;
    private bool _loaded;

    public UserScopeContext(
        IHttpContextAccessor httpContextAccessor,
        IUserScopeService scopeService,
        TenantContext tenantContext,
        IOptions<AuthorizationOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeService = scopeService;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public bool IsLoaded => _loaded;

    public bool ScopesEnabled => _options.EnableDataScopes;

    public DataScopeType ScopeType => _scope?.ScopeType ?? DataScopeType.None;

    public bool IsGlobalScope => !_options.EnableDataScopes || (_scope?.IsGlobalScope ?? false);

    public int ScopeVersion => _scope?.ScopeVersion ?? 1;

    public IReadOnlyList<Guid> AllowedClassIds => _scope?.AllowedClassIds ?? [];

    public IReadOnlyList<Guid> AllowedSubjectIds => _scope?.AllowedSubjectIds ?? [];

    public IReadOnlyList<Guid> AllowedStudentIds => _scope?.AllowedStudentIds ?? [];

    public IReadOnlyList<Guid> AllowedDepartmentIds => _scope?.AllowedDepartmentIds ?? [];

    public IReadOnlyList<Guid> AllowedEmployeeIds => _scope?.AllowedEmployeeIds ?? [];

    public Guid? OwnStudentId => _scope?.OwnStudentId;

    public Guid? ActiveAcademicYearId => _scope?.ActiveAcademicYearId;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            _scope = new UserScopeDto { ScopeType = DataScopeType.None };
            _loaded = true;
            return;
        }

        Guid? schoolId = Guid.TryParse(_tenantContext.SchoolId, out Guid sid) ? sid : null;
        _scope = await _scopeService.GetScopeAsync(userId.Value, schoolId, cancellationToken).ConfigureAwait(false);
        _loaded = true;
    }

    public bool HasClassAccess(Guid classId) =>
        IsGlobalScope || AllowedClassIds.Contains(classId);

    public bool HasSubjectAccess(Guid subjectId) =>
        IsGlobalScope || AllowedSubjectIds.Contains(subjectId);

    public bool HasSubjectInClassAccess(Guid classId, Guid subjectId) =>
        IsGlobalScope || (HasClassAccess(classId) && HasSubjectAccess(subjectId));

    public bool HasStudentAccess(Guid studentId) =>
        IsGlobalScope || AllowedStudentIds.Contains(studentId);

    public bool HasDepartmentAccess(Guid departmentId) =>
        IsGlobalScope || AllowedDepartmentIds.Contains(departmentId);

    public bool HasEmployeeAccess(Guid employeeId) =>
        IsGlobalScope || AllowedEmployeeIds.Contains(employeeId);

    private Guid? GetCurrentUserId()
    {
        string? sub = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
