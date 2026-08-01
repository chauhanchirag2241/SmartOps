using System.Data;
using Dapper;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Class.Interfaces;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;

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

        IEnumerable<MappingLookupOptionDto> classes = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT c.id AS Id,
       trim(cg.classname || COALESCE(' - ' || NULLIF(trim(c.section), ''), '')) AS Name,
       c.section AS SubLabel
FROM {schema}.{DatabaseConfig.TableClasses} c
INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
WHERE c.isactive = true{classBranchFilter}
ORDER BY cg.classname, c.section
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
        => GetByClassIdAsync(classId, academicYearId, cancellationToken);

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

        return await _repository
            .GetByEmployeeIdAsync(employeeId, yearId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
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
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope && !_scope.HasClassAccess(classId))
        {
            return [];
        }

        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await _repository
            .GetByClassIdAsync(classId, yearId, cancellationToken)
            .ConfigureAwait(false);

        return await FilterMappingsForScopeAsync(classId, rows, yearId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto> AddMappingAsync(
        CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClassId == Guid.Empty || request.SubjectId == Guid.Empty)
        {
            throw new InvalidOperationException("Class and subject are required.");
        }

        try
        {
            Guid academicYearId = request.AcademicYearId != Guid.Empty
                ? await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken).ConfigureAwait(false)
                : await ResolveAcademicYearForClassAsync(request.ClassId, cancellationToken).ConfigureAwait(false);

            bool classExists = await _repository
                .ExistsActiveClassAsync(request.ClassId, cancellationToken)
                .ConfigureAwait(false);
            if (!classExists)
            {
                throw new InvalidOperationException("Class not found or is inactive.");
            }

            Guid? employeeId = NormalizeEmployeeId(request.EmployeeId);

            ClassSubjectTeacherMappingEntity? existing = await _repository
                .FindByClassSubjectYearAsync(request.ClassId, request.SubjectId, academicYearId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is { IsActive: true })
            {
                if (employeeId.HasValue)
                {
                    // Upsert teacher onto existing active mapping for class+subject+year.
                    existing.EmployeeId = employeeId;
                    await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
                    await BumpEmployeeScopeIfLinkedAsync(employeeId.Value, cancellationToken).ConfigureAwait(false);
                    return await RequireDtoByIdAsync(existing.Id, cancellationToken).ConfigureAwait(false);
                }

                throw new InvalidOperationException("This subject is already mapped to the selected class.");
            }

            Guid mappingId;
            if (existing is not null)
            {
                existing.EmployeeId = employeeId;
                existing.IsActive = true;
                await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
                mappingId = existing.Id;
            }
            else
            {
                mappingId = await _repository.InsertAsync(
                    new ClassSubjectTeacherMappingEntity
                    {
                        ClassId = request.ClassId,
                        SubjectId = request.SubjectId,
                        EmployeeId = employeeId,
                        AcademicYearId = academicYearId
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            ClassSubjectTeacherMappingDto created = await RequireDtoByIdAsync(mappingId, cancellationToken)
                .ConfigureAwait(false);

            if (employeeId.HasValue)
            {
                await BumpEmployeeScopeIfLinkedAsync(employeeId.Value, cancellationToken).ConfigureAwait(false);
            }

            return created;
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
            throw new InvalidOperationException("Add at least one class and subject permission.");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var created = new List<ClassSubjectTeacherMappingDto>(request.Mappings.Count);

        foreach (CreateClassSubjectTeacherMappingDto item in request.Mappings)
        {
            if (item.ClassId == Guid.Empty || item.SubjectId == Guid.Empty)
            {
                throw new InvalidOperationException("Class and subject are required for every permission.");
            }

            string key = $"{item.ClassId:D}:{item.SubjectId:D}";
            if (!seenKeys.Add(key))
            {
                throw new InvalidOperationException("Duplicate subject for the same class in this assignment.");
            }

            created.Add(
                await AddMappingAsync(
                        new CreateClassSubjectTeacherMappingDto
                        {
                            ClassId = item.ClassId,
                            SubjectId = item.SubjectId,
                            EmployeeId = request.EmployeeId,
                            AcademicYearId = request.AcademicYearId != Guid.Empty
                                ? request.AcademicYearId
                                : item.AcademicYearId
                        },
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        foreach (Guid sectionId in (request.ClassTeacherClassIds ?? []).Distinct())
        {
            if (sectionId == Guid.Empty) continue;
            Guid? classGroupId = await ResolveClassGroupIdAsync(sectionId, cancellationToken).ConfigureAwait(false);
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

    public async Task<ClassSubjectTeacherMappingDto> SetClassTeacherAsync(
        Guid id,
        bool isClassTeacher,
        CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity.EmployeeId is null || entity.EmployeeId == Guid.Empty)
        {
            throw new InvalidOperationException("Assign a teacher to this subject before setting class teacher.");
        }

        Guid? classGroupId = await ResolveClassGroupIdAsync(entity.ClassId, cancellationToken).ConfigureAwait(false);

        if (!isClassTeacher)
        {
            Guid? current = await _classSettings
                .GetClassTeacherEmployeeIdAsync(entity.ClassId, cancellationToken)
                .ConfigureAwait(false);
            if (current != entity.EmployeeId)
            {
                return await RequireDtoByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }

        await _classSettings
            .UpsertClassTeacherAsync(
                entity.ClassId,
                classGroupId,
                isClassTeacher ? entity.EmployeeId : null,
                cancellationToken)
            .ConfigureAwait(false);

        return await RequireDtoByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);

        Guid? previousEmployeeId = entity.EmployeeId;
        Guid? employeeId = request.AssignLater
            ? null
            : request.EmployeeId.HasValue
                ? NormalizeEmployeeId(request.EmployeeId)
                : entity.EmployeeId;

        if (!request.AssignLater && request.EmployeeId.HasValue && employeeId is null)
        {
            throw new InvalidOperationException("A valid teacher is required unless assign later is selected.");
        }

        if (request.SubjectId.HasValue && request.SubjectId.Value != Guid.Empty && request.SubjectId.Value != entity.SubjectId)
        {
            bool duplicate = await _repository
                .ExistsActiveClassSubjectAsync(
                    entity.ClassId,
                    request.SubjectId.Value,
                    entity.AcademicYearId,
                    entity.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (duplicate)
            {
                throw new InvalidOperationException("This subject is already mapped to the selected class.");
            }

            entity.SubjectId = request.SubjectId.Value;
        }

        if (request.EmployeeId.HasValue || request.AssignLater)
        {
            entity.EmployeeId = employeeId;
        }

        try
        {
            entity.IsActive = true;
            int rowsUpdated = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            if (rowsUpdated == 0)
            {
                entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);
                rowsUpdated = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }

            if (rowsUpdated == 0)
            {
                throw new InvalidOperationException("Mapping could not be updated. Please refresh and try again.");
            }
            await BumpEmployeeChangesAsync(previousEmployeeId, entity.EmployeeId, cancellationToken).ConfigureAwait(false);

            return await RequireDtoByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MapDatabaseException(ex) is InvalidOperationException mapped)
        {
            throw mapped;
        }
    }

    public async Task<ClassSubjectTeacherMappingDto> AssignTeacherLaterAsync(
        Guid id,
        AssignTeacherLaterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await UpdateMappingAsync(
            id,
            new UpdateClassSubjectTeacherMappingDto
            {
                AssignLater = request.AssignLater,
                EmployeeId = request.EmployeeId
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteMappingAsync(Guid id, CancellationToken cancellationToken = default)
        => RemoveMappingAsync(id, cancellationToken);

    public async Task RemoveMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        await _repository.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (entity?.EmployeeId is Guid empId)
        {
            await BumpEmployeeScopeIfLinkedAsync(empId, cancellationToken).ConfigureAwait(false);
        }
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

    private async Task<Guid> ResolveAcademicYearForClassAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        bool classExists = await _repository
            .ExistsActiveClassAsync(classId, cancellationToken)
            .ConfigureAwait(false);

        if (!classExists)
        {
            throw new InvalidOperationException("Class not found or is inactive.");
        }

        return await ResolveAcademicYearIdAsync(null, cancellationToken).ConfigureAwait(false);
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
            PostgresErrorCodes.NotNullViolation when pg.ColumnName == "EmployeeId" =>
                new InvalidOperationException(
                    "Cannot save without a teacher. Assign a teacher, or run database migration S111 to allow \"Assign later\"."),
            PostgresErrorCodes.NotNullViolation =>
                new InvalidOperationException("A required mapping field is missing."),
            PostgresErrorCodes.UniqueViolation =>
                new InvalidOperationException("This subject is already mapped to the selected class."),
            PostgresErrorCodes.ForeignKeyViolation =>
                new InvalidOperationException("Invalid class, subject, or teacher reference."),
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

    private async Task<Guid?> ResolveClassGroupIdAsync(Guid classId, CancellationToken cancellationToken)
    {
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT classgroupid FROM {_context.OperationalSchema}.{DatabaseConfig.TableClasses}
WHERE id = @ClassId AND isactive = true LIMIT 1
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task<ClassSubjectTeacherMappingEntity> GetRequiredEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new InvalidOperationException("Mapping not found.");
    }

    private static Guid? NormalizeEmployeeId(Guid? employeeId)
    {
        if (!employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return null;
        }

        return employeeId;
    }

    private async Task BumpEmployeeChangesAsync(
        Guid? previousEmployeeId,
        Guid? currentEmployeeId,
        CancellationToken cancellationToken)
    {
        if (previousEmployeeId.HasValue && previousEmployeeId != currentEmployeeId)
        {
            await BumpEmployeeScopeIfLinkedAsync(previousEmployeeId.Value, cancellationToken).ConfigureAwait(false);
        }

        if (currentEmployeeId.HasValue)
        {
            await BumpEmployeeScopeIfLinkedAsync(currentEmployeeId.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Guid> ResolveAcademicYearIdAsync(Guid? academicYearId, CancellationToken cancellationToken)
    {
        if (academicYearId.HasValue)
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

        classes.RemoveAll(c => !allowedClassIds.Contains(c.Id));
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

    private async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> FilterMappingsForScopeAsync(
        Guid classId,
        IReadOnlyList<ClassSubjectTeacherMappingDto> rows,
        Guid? academicYearId,
        CancellationToken cancellationToken)
    {
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

            HashSet<(Guid ClassId, Guid SubjectId)> pairSet = pairs.ToHashSet();
            return rows.Where(r => pairSet.Contains((classId, r.SubjectId))).ToList();
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
