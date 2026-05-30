using System.Data;
using Dapper;
using Npgsql;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Teacher;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;

namespace SmartOps.Infrastructure.Modules.Teacher.Services;

public sealed class ClassSubjectTeacherMappingService : IClassSubjectTeacherMappingService
{
    private readonly IClassSubjectTeacherMappingRepository _repository;
    private readonly IScopeMappingRepository _scopeMapping;
    private readonly IUserScopeService _userScopeService;
    private readonly IUserScopeContext _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly DapperContext _context;

    public ClassSubjectTeacherMappingService(
        IClassSubjectTeacherMappingRepository repository,
        IScopeMappingRepository scopeMapping,
        IUserScopeService userScopeService,
        IUserScopeContext scope,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        DapperContext context)
    {
        _repository = repository;
        _scopeMapping = scopeMapping;
        _userScopeService = userScopeService;
        _scope = scope;
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
SELECT id AS Id,
       trim(classname || COALESCE(' - ' || NULLIF(trim(
           CASE c.section WHEN 1 THEN 'A' WHEN 2 THEN 'B' WHEN 3 THEN 'C' WHEN 4 THEN 'D' ELSE '' END
       ), ''), '')) AS Name,
       CASE c.section WHEN 1 THEN 'A' WHEN 2 THEN 'B' WHEN 3 THEN 'C' WHEN 4 THEN 'D' ELSE '' END AS SubLabel
FROM {schema}.{DatabaseConfig.TableClasses} c
WHERE isactive = true AND academicyearid = @AcademicYearId
ORDER BY classname, section
""",
                    new { AcademicYearId = yearId },
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        IEnumerable<MappingLookupOptionDto> subjects = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT id AS Id, subjectname AS Name, subjectcode AS Code
FROM {schema}.{DatabaseConfig.TableSubjects}
WHERE isactive = true
ORDER BY subjectname
""",
                    cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        IEnumerable<MappingLookupOptionDto> teachers = await connection
            .QueryAsync<MappingLookupOptionDto>(
                new CommandDefinition(
                    $"""
SELECT id AS Id, trim(firstname || ' ' || lastname) AS Name
FROM {schema}.{DatabaseConfig.TableTeachers}
WHERE isactive = true
ORDER BY firstname, lastname
""",
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
            Teachers = teachers.ToList(),
            ClassSummaries = summaryList
        };
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
            Guid academicYearId = await ResolveAcademicYearForClassAsync(request.ClassId, cancellationToken)
                .ConfigureAwait(false);

            Guid? teacherId = NormalizeTeacherId(request.TeacherId);

            ClassSubjectTeacherMappingEntity? existing = await _repository
                .FindByClassSubjectYearAsync(request.ClassId, request.SubjectId, academicYearId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is { IsActive: true })
            {
                throw new InvalidOperationException("This subject is already mapped to the selected class.");
            }

            if (request.IsClassTeacher)
            {
                await _repository.ClearClassTeacherFlagAsync(request.ClassId, academicYearId, cancellationToken)
                    .ConfigureAwait(false);
            }

            Guid mappingId;
            if (existing is not null)
            {
                existing.TeacherId = teacherId;
                existing.IsClassTeacher = request.IsClassTeacher;
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
                        TeacherId = teacherId,
                        AcademicYearId = academicYearId,
                        IsClassTeacher = request.IsClassTeacher
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            ClassSubjectTeacherMappingDto created = await RequireDtoByIdAsync(mappingId, cancellationToken)
                .ConfigureAwait(false);

            if (teacherId.HasValue)
            {
                await BumpTeacherScopeIfLinkedAsync(teacherId.Value, cancellationToken).ConfigureAwait(false);
            }

            return created;
        }
        catch (Exception ex) when (MapDatabaseException(ex) is InvalidOperationException mapped)
        {
            throw mapped;
        }
    }

    public async Task<ClassSubjectTeacherMappingDto> SetClassTeacherAsync(
        Guid id,
        bool isClassTeacher,
        CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);

        bool saved = await _repository
            .SetClassTeacherFlagAsync(
                id,
                entity.ClassId,
                entity.AcademicYearId,
                isClassTeacher,
                cancellationToken)
            .ConfigureAwait(false);

        if (!saved)
        {
            throw new InvalidOperationException("Could not update class teacher flag.");
        }

        return await RequireDtoByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        bool classTeacherOnly = request.IsClassTeacher.HasValue
            && !request.TeacherId.HasValue
            && !request.AssignLater;

        if (classTeacherOnly)
        {
            return await SetClassTeacherAsync(id, request.IsClassTeacher!.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        ClassSubjectTeacherMappingEntity entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);

        Guid? previousTeacherId = entity.TeacherId;
        Guid? teacherId = request.AssignLater
            ? null
            : request.TeacherId.HasValue
                ? NormalizeTeacherId(request.TeacherId)
                : entity.TeacherId;

        if (!request.AssignLater && request.TeacherId.HasValue && teacherId is null)
        {
            throw new InvalidOperationException("A valid teacher is required unless assign later is selected.");
        }

        if (request.TeacherId.HasValue || request.AssignLater)
        {
            entity.TeacherId = teacherId;
        }

        if (request.IsClassTeacher.HasValue)
        {
            if (request.IsClassTeacher.Value)
            {
                await _repository
                    .ClearClassTeacherFlagAsync(entity.ClassId, entity.AcademicYearId, cancellationToken)
                    .ConfigureAwait(false);

                // ClearClassTeacherFlagAsync bumps versionno on all class rows — reload before update.
                entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);
            }

            entity.IsClassTeacher = request.IsClassTeacher.Value;
        }

        try
        {
            entity.IsActive = true;
            int rowsUpdated = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            if (rowsUpdated == 0)
            {
                entity = await GetRequiredEntityAsync(id, cancellationToken).ConfigureAwait(false);
                if (request.IsClassTeacher.HasValue)
                {
                    entity.IsClassTeacher = request.IsClassTeacher.Value;
                }

                rowsUpdated = await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            }

            if (rowsUpdated == 0)
            {
                throw new InvalidOperationException("Mapping could not be updated. Please refresh and try again.");
            }
            await BumpTeacherChangesAsync(previousTeacherId, entity.TeacherId, cancellationToken).ConfigureAwait(false);

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
                TeacherId = request.TeacherId
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        await _repository.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (entity?.TeacherId is Guid teacherId)
        {
            await BumpTeacherScopeIfLinkedAsync(teacherId, cancellationToken).ConfigureAwait(false);
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
        Guid? classYearId = await _repository
            .GetClassAcademicYearIdAsync(classId, cancellationToken)
            .ConfigureAwait(false);

        if (!classYearId.HasValue)
        {
            throw new InvalidOperationException("Class not found or is inactive.");
        }

        return classYearId.Value;
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
            PostgresErrorCodes.NotNullViolation when pg.ColumnName == "teacherid" =>
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

    private async Task<ClassSubjectTeacherMappingEntity> GetRequiredEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new InvalidOperationException("Mapping not found.");
    }

    private static Guid? NormalizeTeacherId(Guid? teacherId)
    {
        if (!teacherId.HasValue || teacherId.Value == Guid.Empty)
        {
            return null;
        }

        return teacherId;
    }

    private async Task BumpTeacherChangesAsync(
        Guid? previousTeacherId,
        Guid? currentTeacherId,
        CancellationToken cancellationToken)
    {
        if (previousTeacherId.HasValue && previousTeacherId != currentTeacherId)
        {
            await BumpTeacherScopeIfLinkedAsync(previousTeacherId.Value, cancellationToken).ConfigureAwait(false);
        }

        if (currentTeacherId.HasValue)
        {
            await BumpTeacherScopeIfLinkedAsync(currentTeacherId.Value, cancellationToken).ConfigureAwait(false);
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

    private async Task BumpTeacherScopeIfLinkedAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return;
        }

        string sql = $"""
SELECT userid FROM {_context.OperationalSchema}.teachers
WHERE id = @TeacherId AND userid IS NOT NULL AND isactive = true
LIMIT 1
""";
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        Guid? userId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { TeacherId = teacherId }, cancellationToken: cancellationToken));

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
