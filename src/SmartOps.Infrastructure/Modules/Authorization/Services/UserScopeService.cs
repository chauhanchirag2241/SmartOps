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

        IList<string> roleCodes = await _userRepository.GetRoleCodesAsync(userId, cancellationToken).ConfigureAwait(false);

        if (roleCodes.Any(c => RoleCodes.GlobalScopeRoles.Contains(c)))
        {
            int version = schoolId.HasValue
                ? await GetScopeVersionAsync(userId, schoolId.Value, cancellationToken).ConfigureAwait(false)
                : 1;
            UserScopeDto global = GlobalScope(version, academicYearId);
            _cache.Set(cacheKey, global, TimeSpan.FromMinutes(_options.ScopeCacheMinutes));
            return global;
        }

        int scopeVersion = schoolId.HasValue
            ? await GetScopeVersionAsync(userId, schoolId.Value, cancellationToken).ConfigureAwait(false)
            : 1;

        UserScopeDto scope;

        if (roleCodes.Contains(RoleCodes.Hod, StringComparer.OrdinalIgnoreCase))
        {
            scope = await ResolveHodScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (roleCodes.Contains(RoleCodes.Teacher, StringComparer.OrdinalIgnoreCase))
        {
            scope = await ResolveTeacherScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (roleCodes.Contains(RoleCodes.Student, StringComparer.OrdinalIgnoreCase))
        {
            scope = await ResolveStudentScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (roleCodes.Contains(RoleCodes.Parent, StringComparer.OrdinalIgnoreCase))
        {
            scope = await ResolveParentScopeAsync(userId, schema, academicYearId, scopeVersion, cancellationToken).ConfigureAwait(false);
        }
        else if (roleCodes.Contains(RoleCodes.Accountant, StringComparer.OrdinalIgnoreCase))
        {
            scope = ModuleOnlyScope(scopeVersion, academicYearId);
        }
        else if (roleCodes.Contains(RoleCodes.Staff, StringComparer.OrdinalIgnoreCase))
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

    public async Task<int> GetScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT version FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserScopeVersions}
WHERE userid = @UserId AND schoolid = @SchoolId
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        int? version = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(sql, new { UserId = userId, SchoolId = schoolId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return version ?? 1;
    }

    public async Task BumpScopeVersionAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserScopeVersions} (userid, schoolid, version, updatedon)
VALUES (@UserId, @SchoolId, 1, NOW())
ON CONFLICT (userid) DO UPDATE SET
    version = {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserScopeVersions}.version + 1,
    schoolid = EXCLUDED.schoolid,
    updatedon = NOW()
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, SchoolId = schoolId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        string schema = _context.OperationalSchema;
        _cache.Remove($"scope:{userId}:{schoolId}:{schema}");
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
SELECT DISTINCT m.classid
FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableEmployees} t ON t.id = m.employeeid
WHERE t.departmentid = ANY(@DepartmentIds)
  AND m.isactive = true
  AND t.isactive = true
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
        await _scopeMapping
            .EnsureEmployeeLinkedToUserAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

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

    private async Task<UserScopeDto> ResolveParentScopeAsync(
        Guid userId,
        string schema,
        Guid? academicYearId,
        int scopeVersion,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> studentIds = await _scopeMapping
            .GetLinkedStudentIdsForParentAsync(schema, userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> classIds = [];
        if (studentIds.Count > 0)
        {
            string sql = $"""
SELECT DISTINCT classid FROM {schema}.{DatabaseConfig.TableStudentAcademics}
WHERE studentid = ANY(@StudentIds) AND isactive = true
  AND (@AcademicYearId IS NULL OR academicyearid = @AcademicYearId)
""";
            IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
            classIds = (await connection.QueryAsync<Guid>(
                new CommandDefinition(sql, new { StudentIds = studentIds.ToArray(), AcademicYearId = academicYearId }, cancellationToken: cancellationToken))
                .ConfigureAwait(false)).Distinct().ToList();
        }

        return new UserScopeDto
        {
            ScopeType = DataScopeType.LinkedStudents,
            ScopeVersion = scopeVersion,
            IsGlobalScope = false,
            AllowedStudentIds = studentIds,
            AllowedClassIds = classIds,
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
