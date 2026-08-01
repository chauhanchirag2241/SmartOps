using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.AcademicYear;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Enums;
using Dapper;
using System.Data;

namespace SmartOps.Infrastructure.Modules.Authorization.Services;

public sealed class UserScopeService : IUserScopeService
{
    private readonly IUserRepository _userRepository;
    private readonly IScopeMappingRepository _scopeMapping;
    private readonly IAcademicYearContext _academicYearContext;
    private readonly DapperContext _context;
    private readonly TenantContext _tenantContext;
    private readonly IMemoryCache _cache;
    private readonly AuthorizationOptions _options;

    public UserScopeService(
        IUserRepository userRepository,
        IScopeMappingRepository scopeMapping,
        IAcademicYearContext academicYearContext,
        DapperContext context,
        TenantContext tenantContext,
        IMemoryCache cache,
        IOptions<AuthorizationOptions> options)
    {
        _userRepository = userRepository;
        _scopeMapping = scopeMapping;
        _academicYearContext = academicYearContext;
        _context = context;
        _tenantContext = tenantContext;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<UserScopeDto> GetScopeAsync(
        Guid userId,
        Guid? schoolId,
        CancellationToken cancellationToken = default)
    {
        string schema = _context.OperationalSchema;
        await _academicYearContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        Guid? academicYearId = _academicYearContext.EffectiveAcademicYearId
            ?? await _scopeMapping.GetActiveAcademicYearIdAsync(schema, cancellationToken).ConfigureAwait(false);

        if (!_options.EnableDataScopes)
        {
            return GlobalScope(1, academicYearId);
        }

        string cacheKey = $"scope:{userId}:{schoolId}:{schema}:{academicYearId}";

        if (_cache.TryGetValue(cacheKey, out UserScopeDto? cached) && cached is not null)
        {
            return cached;
        }

        string? userTypeCode = await _userRepository.GetUserTypeCodeAsync(userId, cancellationToken).ConfigureAwait(false);

        if (UserTypeCodes.IsGlobalScope(userTypeCode))
        {
            UserScopeDto global = GlobalScope(1, academicYearId);
            _cache.Set(cacheKey, global, TimeSpan.FromMinutes(_options.ScopeCacheMinutes));
            return global;
        }

        const int scopeVersion = 1;

        UserScopeDto scope;

        if (string.Equals(userTypeCode, UserTypeCodes.Teacher, StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<Guid> hodDepartments = await _scopeMapping
                .GetDepartmentIdsForHodAsync(schema, userId, cancellationToken)
                .ConfigureAwait(false);
            scope = hodDepartments.Count > 0
                ? await ResolveHodScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false)
                : await ResolveTeacherScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(userTypeCode, UserTypeCodes.Student, StringComparison.OrdinalIgnoreCase))
        {
            scope = await ResolveStudentScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(userTypeCode, UserTypeCodes.Accountant, StringComparison.OrdinalIgnoreCase))
        {
            scope = ModuleOnlyScope(scopeVersion, academicYearId);
        }
        else if (string.Equals(userTypeCode, UserTypeCodes.NonAcademicStaff, StringComparison.OrdinalIgnoreCase))
        {
            scope = await ResolveStaffScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            scope = EmptyScope(scopeVersion, academicYearId);
        }

        _cache.Set(cacheKey, scope, TimeSpan.FromMinutes(_options.ScopeCacheMinutes));
        return scope;
    }

    public Task<int> GetScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default)
    {
        _ = userId;
        _ = schoolId;
        _ = cancellationToken;
        return Task.FromResult(1);
    }

    public Task BumpScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        string schema = _context.OperationalSchema;
        // Cache keys include academic year; TTL also expires entries (ScopeCacheMinutes).
        _cache.Remove($"scope:{userId}:{schoolId}:{schema}");
        return Task.CompletedTask;
    }

    private async Task<UserScopeDto> ResolveHodScopeAsync(
        Guid userId,
        string schema,
        Guid? academicYearId,
        int scopeVersion,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> departmentIds = await _scopeMapping
            .GetDepartmentIdsForHodAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> classIds = await _scopeMapping
            .GetClassIdsByDepartmentsAsync(schema, departmentIds, cancellationToken)
            .ConfigureAwait(false);

        if (classIds.Count == 0 && departmentIds.Count > 0)
        {
            classIds = await GetClassIdsFromDepartmentTeachersAsync(schema, departmentIds, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Guid> employeeids = await _scopeMapping
            .GetEmployeeIdsByDepartmentsAsync(schema, departmentIds, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> studentIds = await _scopeMapping
            .GetStudentIdsByClassIdsAsync(schema, classIds, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return new UserScopeDto
        {
            ScopeType = DataScopeType.Department,
            ScopeVersion = scopeVersion,
            IsGlobalScope = false,
            AllowedDepartmentIds = departmentIds,
            AllowedClassIds = classIds,
            AllowedEmployeeIds = employeeids,
            AllowedStudentIds = studentIds,
            ActiveAcademicYearId = academicYearId
        };
    }

    private async Task<IReadOnlyList<Guid>> GetClassIdsFromDepartmentTeachersAsync(
        string schema,
        IReadOnlyList<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT DISTINCT ct.classid
FROM {schema}.{DatabaseConfig.TableClassTimetables} ct
INNER JOIN {schema}.{DatabaseConfig.TableClassTimetableSlots} s ON s.timetableid = ct.id
INNER JOIN {schema}.{DatabaseConfig.TableEmployees} t ON t.id = s.employeeid
WHERE t.departmentid = ANY(@DepartmentIds)
  AND ct.isactive = true
  AND s.isactive = true
  AND t.isactive = true
  AND s.employeeid IS NOT NULL
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { DepartmentIds = departmentIds.ToArray() }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.Distinct().ToList();
    }

    private async Task<UserScopeDto> ResolveTeacherScopeAsync(
        Guid userId,
        string schema,
        Guid? academicYearId,
        int scopeVersion,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> classIds = await ResolveTeacherClassIdsWithFallbackAsync(
            schema, userId, academicYearId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Guid> subjectIds = await ResolveTeacherSubjectIdsWithFallbackAsync(
            schema, userId, academicYearId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Guid> studentIds = await _scopeMapping
            .GetStudentIdsByClassIdsAsync(schema, classIds, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return new UserScopeDto
        {
            ScopeType = DataScopeType.Class,
            ScopeVersion = scopeVersion,
            IsGlobalScope = false,
            AllowedClassIds = classIds,
            AllowedSubjectIds = subjectIds,
            AllowedStudentIds = studentIds,
            ActiveAcademicYearId = academicYearId
        };
    }

    private async Task<IReadOnlyList<Guid>> ResolveTeacherClassIdsWithFallbackAsync(
        string schema,
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> classIds = await _scopeMapping
            .GetEmployeeClassIdsAsync(schema, userId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        if (classIds.Count == 0 && academicYearId.HasValue)
        {
            classIds = await _scopeMapping
                .GetEmployeeClassIdsAsync(schema, userId, null, cancellationToken)
                .ConfigureAwait(false);
        }

        return classIds;
    }

    private async Task<IReadOnlyList<Guid>> ResolveTeacherSubjectIdsWithFallbackAsync(
        string schema,
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> subjectIds = await _scopeMapping
            .GetEmployeeSubjectIdsAsync(schema, userId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        if (subjectIds.Count == 0 && academicYearId.HasValue)
        {
            subjectIds = await _scopeMapping
                .GetEmployeeSubjectIdsAsync(schema, userId, null, cancellationToken)
                .ConfigureAwait(false);
        }

        return subjectIds;
    }

    private async Task<UserScopeDto> ResolveStudentScopeAsync(
        Guid userId,
        string schema,
        Guid? academicYearId,
        int scopeVersion,
        CancellationToken cancellationToken)
    {
        Guid? studentId = await _scopeMapping
            .GetStudentIdByUserIdAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

        return new UserScopeDto
        {
            ScopeType = DataScopeType.Self,
            ScopeVersion = scopeVersion,
            IsGlobalScope = false,
            OwnStudentId = studentId,
            AllowedStudentIds = studentId.HasValue ? [studentId.Value] : [],
            ActiveAcademicYearId = academicYearId
        };
    }

    private async Task<UserScopeDto> ResolveStaffScopeAsync(
        Guid userId,
        string schema,
        Guid? academicYearId,
        int scopeVersion,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> classIds = await _scopeMapping
            .GetStaffScopeClassIdsAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> departmentIds = await _scopeMapping
            .GetStaffScopeDepartmentIdsAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

        if (classIds.Count == 0 && departmentIds.Count > 0)
        {
            classIds = await GetClassIdsFromDepartmentTeachersAsync(schema, departmentIds, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Guid> studentIds = await _scopeMapping
            .GetStudentIdsByClassIdsAsync(schema, classIds, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return new UserScopeDto
        {
            ScopeType = DataScopeType.Custom,
            ScopeVersion = scopeVersion,
            IsGlobalScope = false,
            AllowedClassIds = classIds,
            AllowedDepartmentIds = departmentIds,
            AllowedStudentIds = studentIds,
            ActiveAcademicYearId = academicYearId
        };
    }

    private static UserScopeDto GlobalScope(int version, Guid? academicYearId) => new()
    {
        ScopeType = DataScopeType.Global,
        ScopeVersion = version,
        IsGlobalScope = true,
        ActiveAcademicYearId = academicYearId
    };

    private static UserScopeDto ModuleOnlyScope(int version, Guid? academicYearId) => new()
    {
        ScopeType = DataScopeType.ModuleOnly,
        ScopeVersion = version,
        IsGlobalScope = false,
        ActiveAcademicYearId = academicYearId
    };

    private static UserScopeDto EmptyScope(int version, Guid? academicYearId) => new()
    {
        ScopeType = DataScopeType.None,
        ScopeVersion = version,
        IsGlobalScope = false,
        ActiveAcademicYearId = academicYearId
    };
}
