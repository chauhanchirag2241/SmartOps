using System.Data;
using Dapper;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Class.Interfaces;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Teacher.Services;

public sealed class ClassSubjectTeacherMappingService : IClassSubjectTeacherMappingService
{
    private readonly IClassSubjectTeacherMappingRepository _repository;
    private readonly IClassSettingRepository _classSettings;
    private readonly IScopeMappingRepository _scopeMapping;
    private readonly IUserScopeService _userScopeService;
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly DapperContext _context;

    public ClassSubjectTeacherMappingService(
        IClassSubjectTeacherMappingRepository repository,
        IClassSettingRepository classSettings,
        IScopeMappingRepository scopeMapping,
        IUserScopeService userScopeService,
        IUserScopeContext scope,
        IBranchContext branchContext,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        DapperContext context)
    {
        _repository = repository;
        _classSettings = classSettings;
        _scopeMapping = scopeMapping;
        _userScopeService = userScopeService;
        _scope = scope;
        _branchContext = branchContext;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _context = context;
    }

    public async Task<MappingLookupsResponseDto> GetLookupsAsync(
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        Guid yearId = await ResolveAcademicYearIdAsync(academicYearId, cancellationToken).ConfigureAwait(false);
        string schema = _context.OperationalSchema;
        (string classBranchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "cg", cancellationToken)
            .ConfigureAwait(false);
        (string subjectBranchFilter, _) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "s", cancellationToken)
            .ConfigureAwait(false);
        (string employeeBranchFilter, _) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "e", cancellationToken)
            .ConfigureAwait(false);

        IDbConnection connection = await _context
            .GetGlobalConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<MappingLookupOptionDto> academicYears = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT id AS Id, title AS Name
FROM {schema}.{DatabaseConfig.TableAcademicYears}
WHERE isactive = true
ORDER BY startdate DESC
""",
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        // Class groups exposed as "Classes" for teacher-assign UI simplicity.
        IEnumerable<MappingLookupOptionDto> classes = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT cg.id AS Id, cg.classname AS Name
FROM {schema}.{DatabaseConfig.TableClassGroups} cg
WHERE cg.isactive = true{classBranchFilter}
ORDER BY cg.classname
""",
                    new { ActiveBranchId = activeBranchId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        IEnumerable<MappingLookupOptionDto> subjects = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT s.id AS Id, s.subjectname AS Name, s.subjectcode AS Code
FROM {schema}.{DatabaseConfig.TableSubjects} s
WHERE s.isactive = true{subjectBranchFilter}
ORDER BY s.subjectname
""",
                    new { ActiveBranchId = activeBranchId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        IEnumerable<MappingLookupOptionDto> teachers = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT e.id AS Id, trim(u.firstname || ' ' || u.lastname) AS Name
FROM {schema}.{DatabaseConfig.TableEmployees} e
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
WHERE e.isactive = true{employeeBranchFilter}
ORDER BY u.firstname, u.lastname
""",
                    new { ActiveBranchId = activeBranchId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        IReadOnlyList<ClassMappingSummaryDto> summaries = await _repository
            .GetClassSummariesAsync(yearId, cancellationToken)
            .ConfigureAwait(false);

        List<MappingLookupOptionDto> classList = classes.ToList();
        List<MappingLookupOptionDto> subjectList = subjects.ToList();
        List<ClassMappingSummaryDto> summaryList = summaries.ToList();

        await ApplyMappingLookupsScopeAsync(classList, subjectList, summaryList, yearId, cancellationToken)
            .ConfigureAwait(false);

        return new MappingLookupsResponseDto
        {
            ActiveAcademicYearId = yearId,
            AcademicYears = academicYears.ToList(),
            Classes = classList,
            Subjects = subjectList,
            Employees = teachers.ToList(),
            ClassSummaries = summaryList
        };
    }

    public Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
        => GetByClassGroupIdAsync(classId, academicYearId, cancellationToken);

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByEmployeeAsync(
        Guid employeeId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        Guid? yearId = academicYearId;
        if (!yearId.HasValue)
        {
            yearId = await ResolveAcademicYearIdAsync(null, cancellationToken).ConfigureAwait(false);
        }

        // Include inactive so teacher management UI can show soft-deleted rows.
        return await _repository
            .GetByEmployeeIdAsync(employeeId, yearId, includeInactive: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassGroupIdAsync(
        Guid classGroupId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        Guid? yearId = academicYearId;
        if (!yearId.HasValue)
        {
            await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            yearId = _scope.ActiveAcademicYearId
                ?? await _scopeMapping.GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
                    .ConfigureAwait(false);
        }

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            bool hasAccess = await HasClassGroupAccessAsync(classGroupId, cancellationToken).ConfigureAwait(false);
            if (!hasAccess)
            {
                return [];
            }
        }

        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await _repository
            .GetByClassIdAsync(classGroupId, yearId, cancellationToken)
            .ConfigureAwait(false);

        return await FilterMappingsForScopeAsync(classGroupId, rows, yearId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto> AddMappingAsync(
        CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClassGroupId == Guid.Empty)
        {
            throw new InvalidOperationException("Class group is required.");
        }

        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOperationException("Employee is required.");
        }

        IReadOnlyList<Guid> subjectIds = ResolveCreateSubjectIds(request);
        if (subjectIds.Count == 0)
        {
            throw new InvalidOperationException("At least one subject is required.");
        }

        try
        {
            Guid academicYearId = await ResolveAcademicYearIdAsync(
                    request.AcademicYearId != Guid.Empty ? request.AcademicYearId : null,
                    cancellationToken)
                .ConfigureAwait(false);

            bool classGroupExists = await _repository
                .ExistsActiveClassGroupAsync(request.ClassGroupId, cancellationToken)
                .ConfigureAwait(false);
            if (!classGroupExists)
            {
                throw new InvalidOperationException("Class group not found or is inactive.");
            }

            bool subjectsOk = await _repository
                .AllSubjectsBelongToClassGroupAsync(request.ClassGroupId, subjectIds, cancellationToken)
                .ConfigureAwait(false);
            if (!subjectsOk)
            {
                throw new InvalidOperationException("One or more subjects do not belong to the selected class group.");
            }

            ClassSubjectTeacherMappingDto? last = null;
            foreach (Guid subjectId in subjectIds)
            {
                last = await UpsertSubjectMappingAsync(
                        request.ClassGroupId,
                        subjectId,
                        request.EmployeeId,
                        academicYearId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await BumpEmployeeScopeIfLinkedAsync(request.EmployeeId, cancellationToken).ConfigureAwait(false);

            return last ?? throw new InvalidOperationException("Mapping was saved but could not be loaded.");
        }
        catch (Exception ex) when (MapDatabaseException(ex) is InvalidOperationException mapped)
        {
            throw mapped;
        }
    }

    public async Task<BulkCreateClassSubjectTeacherMappingsResultDto> BulkAddMappingsAsync(
        BulkCreateClassSubjectTeacherMappingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId == Guid.Empty)
        {
            throw new InvalidOperationException("Employee is required.");
        }

        if (request.Mappings is null || request.Mappings.Count == 0)
        {
            throw new InvalidOperationException("Add at least one class group and subject permission.");
        }

        Guid academicYearId = await ResolveAcademicYearIdAsync(
                request.AcademicYearId != Guid.Empty ? request.AcademicYearId : null,
                cancellationToken)
            .ConfigureAwait(false);

        var seenKeys = new HashSet<(Guid ClassGroupId, Guid SubjectId)>();
        var created = new List<ClassSubjectTeacherMappingDto>();

        try
        {
            foreach (BulkClassSubjectTeacherMappingItemDto item in request.Mappings)
            {
                if (item.ClassGroupId == Guid.Empty)
                {
                    throw new InvalidOperationException("Class group is required for every permission.");
                }

                IReadOnlyList<Guid> subjectIds = NormalizeSubjectIds(item.SubjectIds);
                if (subjectIds.Count == 0)
                {
                    throw new InvalidOperationException("At least one subject is required for every class group.");
                }

                bool classGroupExists = await _repository
                    .ExistsActiveClassGroupAsync(item.ClassGroupId, cancellationToken)
                    .ConfigureAwait(false);
                if (!classGroupExists)
                {
                    throw new InvalidOperationException("Class group not found or is inactive.");
                }

                bool subjectsOk = await _repository
                    .AllSubjectsBelongToClassGroupAsync(item.ClassGroupId, subjectIds, cancellationToken)
                    .ConfigureAwait(false);
                if (!subjectsOk)
                {
                    throw new InvalidOperationException("One or more subjects do not belong to the selected class group.");
                }

                foreach (Guid subjectId in subjectIds)
                {
                    if (!seenKeys.Add((item.ClassGroupId, subjectId)))
                    {
                        throw new InvalidOperationException("Duplicate class group and subject in this assignment.");
                    }

                    created.Add(
                        await UpsertSubjectMappingAsync(
                                item.ClassGroupId,
                                subjectId,
                                request.EmployeeId,
                                academicYearId,
                                cancellationToken)
                            .ConfigureAwait(false));
                }
            }

            await BumpEmployeeScopeIfLinkedAsync(request.EmployeeId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MapDatabaseException(ex) is InvalidOperationException mapped)
        {
            throw mapped;
        }

        foreach (Guid sectionId in (request.ClassTeacherClassIds ?? []).Distinct())
        {
            if (sectionId == Guid.Empty)
            {
                continue;
            }

            Guid? classGroupId = await ResolveClassGroupIdForSectionAsync(sectionId, cancellationToken)
                .ConfigureAwait(false);
            await _classSettings
                .UpsertClassTeacherAsync(sectionId, classGroupId, request.EmployeeId, cancellationToken)
                .ConfigureAwait(false);
        }

        return new BulkCreateClassSubjectTeacherMappingsResultDto
        {
            CreatedCount = created.Count,
            Created = created
        };
    }

    public async Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);

        if (request.SubjectId is null && request.IsActive is null)
        {
            throw new InvalidOperationException("Provide SubjectId and/or IsActive to update.");
        }

        if (request.SubjectId.HasValue)
        {
            if (request.SubjectId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Subject is required.");
            }

            bool subjectsOk = await _repository
                .AllSubjectsBelongToClassGroupAsync(entity.ClassGroupId, [request.SubjectId.Value], cancellationToken)
                .ConfigureAwait(false);
            if (!subjectsOk)
            {
                throw new InvalidOperationException("Subject does not belong to the selected class group.");
            }

            entity.SubjectId = request.SubjectId.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }
        else if (request.SubjectId.HasValue)
        {
            // Changing subject reactivates the row.
            entity.IsActive = true;
        }

        try
        {
            int rowsUpdated = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            if (rowsUpdated == 0)
            {
                throw new InvalidOperationException("Mapping could not be updated. Please refresh and try again.");
            }

            await BumpEmployeeScopeIfLinkedAsync(entity.EmployeeId, cancellationToken).ConfigureAwait(false);

            return await RequireDtoByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MapDatabaseException(ex) is InvalidOperationException mapped)
        {
            throw mapped;
        }
    }

    public async Task DeleteMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        await _repository.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (entity is not null && entity.EmployeeId != Guid.Empty)
        {
            await BumpEmployeeScopeIfLinkedAsync(entity.EmployeeId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ClassSubjectTeacherMappingDto> UpsertSubjectMappingAsync(
        Guid classGroupId,
        Guid subjectId,
        Guid employeeId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingEntity? existing = await _repository
            .FindByClassGroupSubjectEmployeeYearAsync(
                classGroupId,
                subjectId,
                employeeId,
                academicYearId,
                cancellationToken)
            .ConfigureAwait(false);

        Guid mappingId;
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            }

            mappingId = existing.Id;
        }
        else
        {
            mappingId = await _repository.InsertAsync(
                new ClassSubjectTeacherMappingEntity
                {
                    ClassGroupId = classGroupId,
                    SubjectId = subjectId,
                    EmployeeId = employeeId,
                    AcademicYearId = academicYearId
                },
                cancellationToken).ConfigureAwait(false);
        }

        return await RequireDtoByIdAsync(mappingId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClassSubjectTeacherMappingDto> RequireDtoByIdAsync(
        Guid mappingId,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingDto? dto = await _repository
            .GetDtoByIdAsync(mappingId, cancellationToken)
            .ConfigureAwait(false);

        return dto ?? throw new InvalidOperationException("Mapping was saved but could not be loaded.");
    }

    private static IReadOnlyList<Guid> ResolveCreateSubjectIds(CreateClassSubjectTeacherMappingDto request)
    {
        var ids = new List<Guid>();
        if (request.SubjectId.HasValue && request.SubjectId.Value != Guid.Empty)
        {
            ids.Add(request.SubjectId.Value);
        }

        ids.AddRange(NormalizeSubjectIds(request.SubjectIds));
        return ids.Distinct().ToList();
    }

    private static IReadOnlyList<Guid> NormalizeSubjectIds(IEnumerable<Guid>? subjectIds)
    {
        if (subjectIds is null)
        {
            return [];
        }

        return subjectIds.Where(id => id != Guid.Empty).Distinct().ToList();
    }

    private static InvalidOperationException? MapDatabaseException(Exception ex)
    {
        PostgresException? pg = FindPostgresException(ex);
        if (pg is null)
        {
            return null;
        }

        return pg.SqlState switch
        {
            PostgresErrorCodes.NotNullViolation =>
                new InvalidOperationException("A required mapping field is missing."),
            PostgresErrorCodes.UniqueViolation =>
                new InvalidOperationException(
                    "This teacher is already mapped to the selected class group and subject for this year."),
            PostgresErrorCodes.ForeignKeyViolation =>
                new InvalidOperationException("Invalid class group, subject, or teacher reference."),
            _ => null
        };
    }

    private static PostgresException? FindPostgresException(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }

            current = current.InnerException;
        }

        return null;
    }

    private async Task<Guid?> ResolveClassGroupIdForSectionAsync(Guid sectionId, CancellationToken cancellationToken)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT classgroupid FROM {_context.OperationalSchema}.{DatabaseConfig.TableClasses}
WHERE id = @ClassId AND isactive = true LIMIT 1
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { ClassId = sectionId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<bool> HasClassGroupAccessAsync(Guid classGroupId, CancellationToken cancellationToken)
    {
        if (_scope.AllowedClassIds.Contains(classGroupId))
        {
            return true;
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return false;
        }

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT EXISTS (
    SELECT 1 FROM {_context.OperationalSchema}.{DatabaseConfig.TableClasses}
    WHERE classgroupid = @ClassGroupId
      AND isactive = true
      AND id = ANY(@AllowedClassIds))
""";
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { ClassGroupId = classGroupId, AllowedClassIds = _scope.AllowedClassIds.ToArray() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<ClassSubjectTeacherMappingEntity> GetRequiredEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new InvalidOperationException("Mapping not found.");
    }

    private async Task<Guid> ResolveAcademicYearIdAsync(Guid? academicYearId, CancellationToken cancellationToken)
    {
        if (academicYearId.HasValue && academicYearId.Value != Guid.Empty)
        {
            return academicYearId.Value;
        }

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_scope.ActiveAcademicYearId.HasValue)
        {
            return _scope.ActiveAcademicYearId.Value;
        }

        Guid? active = await _scopeMapping
            .GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
            .ConfigureAwait(false);

        if (active.HasValue)
        {
            return active.Value;
        }

        string schema = _context.OperationalSchema;
        IDbConnection connection = await _context
            .GetGlobalConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? latest = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                $"""
SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
ORDER BY startdate DESC NULLS LAST, createdon DESC
LIMIT 1
""",
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return latest ?? throw new InvalidOperationException("No academic year found.");
    }

    private async Task BumpEmployeeScopeIfLinkedAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return;
        }

        string sql = $"""
SELECT userid FROM {_context.OperationalSchema}.{DatabaseConfig.TableEmployees}
WHERE id = @EmployeeId AND userid IS NOT NULL AND isactive = true
LIMIT 1
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid? userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: cancellationToken));

        if (userId.HasValue)
        {
            await _userScopeService.BumpScopeVersionAsync(userId.Value, schoolId, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = _tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }

    private async Task ApplyMappingLookupsScopeAsync(
        List<MappingLookupOptionDto> classes,
        List<MappingLookupOptionDto> subjects,
        List<ClassMappingSummaryDto> summaries,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return;
        }

        HashSet<Guid> allowedClassIds = _scope.AllowedClassIds.ToHashSet();
        if (allowedClassIds.Count == 0)
        {
            classes.Clear();
            subjects.Clear();
            summaries.Clear();
            return;
        }

        HashSet<Guid> allowedClassGroupIds = await ResolveClassGroupIdsForSectionsAsync(allowedClassIds, cancellationToken)
            .ConfigureAwait(false);

        classes.RemoveAll(c => !allowedClassGroupIds.Contains(c.Id) && !allowedClassIds.Contains(c.Id));
        summaries.RemoveAll(s => !allowedClassIds.Contains(s.ClassId));

        HashSet<Guid> allowedSubjectIds = await ResolveScopedSubjectIdsAsync(academicYearId, cancellationToken)
            .ConfigureAwait(false);
        if (allowedSubjectIds.Count == 0)
        {
            subjects.Clear();
            return;
        }

        subjects.RemoveAll(s => !allowedSubjectIds.Contains(s.Id));
    }

    private async Task<HashSet<Guid>> ResolveClassGroupIdsForSectionsAsync(
        IReadOnlyCollection<Guid> sectionIds,
        CancellationToken cancellationToken)
    {
        if (sectionIds.Count == 0)
        {
            return [];
        }

        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT DISTINCT classgroupid
FROM {_context.OperationalSchema}.{DatabaseConfig.TableClasses}
WHERE id = ANY(@SectionIds) AND isactive = true AND classgroupid IS NOT NULL
""";
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                sql,
                new { SectionIds = sectionIds.ToArray() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return ids.ToHashSet();
    }

    private async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> FilterMappingsForScopeAsync(
        Guid classGroupId,
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows,
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        _ = classGroupId;
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return rows;
        }

        if (_scope.ScopeType == DataScopeType.Class)
        {
            IReadOnlyList<(Guid ClassId, Guid SubjectId)> pairs = await _repository
                .GetClassSubjectPairsForTeacherUserAsync(_currentUser.UserId, academicYearId, cancellationToken)
                .ConfigureAwait(false);

            HashSet<Guid> allowedSubjects = pairs
                .Where(p => _scope.HasClassAccess(p.ClassId))
                .Select(p => p.SubjectId)
                .ToHashSet();

            return rows.Where(r => allowedSubjects.Contains(r.SubjectId)).ToList();
        }

        HashSet<Guid> allowedSubjectIds = await ResolveScopedSubjectIdsAsync(academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return rows.Where(r => allowedSubjectIds.Contains(r.SubjectId)).ToList();
    }

    private async Task<HashSet<Guid>> ResolveScopedSubjectIdsAsync(
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        if (_scope.AllowedSubjectIds.Count > 0)
        {
            return _scope.AllowedSubjectIds.ToHashSet();
        }

        if (_scope.AllowedClassIds.Count > 0)
        {
            IReadOnlyList<Guid> subjectIds = await _repository
                .GetSubjectIdsForClassIdsAsync(_scope.AllowedClassIds, academicYearId, cancellationToken)
                .ConfigureAwait(false);
            return subjectIds.ToHashSet();
        }

        return [];
    }
}
