using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Application.Modules.Teacher.Interfaces;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Teacher.Services;

public sealed class ClassSubjectTeacherMappingService : IClassSubjectTeacherMappingService
{
    private readonly IClassSubjectTeacherMappingRepository _repository;
    private readonly IScopeMappingRepository _scopeMapping;
    private readonly IUserScopeService _userScopeService;
    private readonly ITenantProvider _tenantProvider;
    private readonly DapperContext _context;

    public ClassSubjectTeacherMappingService(
        IClassSubjectTeacherMappingRepository repository,
        IScopeMappingRepository scopeMapping,
        IUserScopeService userScopeService,
        ITenantProvider tenantProvider,
        DapperContext context)
    {
        _repository = repository;
        _scopeMapping = scopeMapping;
        _userScopeService = userScopeService;
        _tenantProvider = tenantProvider;
        _context = context;
    }

    public async Task<TeacherAssignmentsResponseDto> GetTeacherAssignmentsAsync(
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        Guid? academicYearId = await _scopeMapping
            .GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ClassSubjectTeacherMappingDto> rows = await _repository
            .GetByTeacherIdAsync(teacherId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        return new TeacherAssignmentsResponseDto
        {
            TeacherId = teacherId,
            AcademicYearId = academicYearId,
            ClassAssignments = GroupRowsForTeacher(rows)
        };
    }

    public async Task SaveTeacherAssignmentsAsync(
        Guid teacherId,
        SaveTeacherAssignmentsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Guid academicYearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        await _repository.SoftDeleteByTeacherAsync(teacherId, academicYearId, cancellationToken).ConfigureAwait(false);

        foreach (TeacherClassAssignmentRowDto row in request.ClassAssignments.Where(r => r.ClassId != Guid.Empty))
        {
            bool isClassTeacher = row.IsClassTeacher;
            if (isClassTeacher)
            {
                await _repository.ClearClassTeacherFlagAsync(row.ClassId, academicYearId, cancellationToken)
                    .ConfigureAwait(false);
            }

            IEnumerable<Guid> subjectIds = row.SubjectIds.Count > 0 ? row.SubjectIds : [Guid.Empty];
            foreach (Guid subjectId in subjectIds.Distinct())
            {
                if (subjectId == Guid.Empty)
                {
                    continue;
                }

                await _repository.InsertAsync(
                    new ClassSubjectTeacherMappingEntity
                    {
                        ClassId = row.ClassId,
                        SubjectId = subjectId,
                        TeacherId = teacherId,
                        AcademicYearId = academicYearId,
                        IsClassTeacher = isClassTeacher
                    },
                    cancellationToken).ConfigureAwait(false);

                isClassTeacher = false;
            }
        }

        await BumpTeacherScopeIfLinkedAsync(teacherId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        Guid? yearId = academicYearId
            ?? await _scopeMapping.GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
                .ConfigureAwait(false);

        return await _repository.GetByClassIdAsync(classId, yearId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveClassMappingsAsync(
        Guid classId,
        SaveClassMappingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Guid academicYearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        await _repository.SoftDeleteByClassAsync(classId, academicYearId, cancellationToken).ConfigureAwait(false);
        await _repository.ClearClassTeacherFlagAsync(classId, academicYearId, cancellationToken).ConfigureAwait(false);

        HashSet<Guid> affectedTeachers = [];

        foreach (ClassMappingGridRowDto row in request.Rows.Where(r => r.SubjectId != Guid.Empty))
        {
            foreach (Guid teacherId in row.TeacherIds.Distinct().Where(id => id != Guid.Empty))
            {
                bool isClassTeacher = request.ClassTeacherId.HasValue && request.ClassTeacherId.Value == teacherId;
                if (isClassTeacher)
                {
                    await _repository.ClearClassTeacherFlagAsync(classId, academicYearId, cancellationToken)
                        .ConfigureAwait(false);
                }

                await _repository.InsertAsync(
                    new ClassSubjectTeacherMappingEntity
                    {
                        ClassId = classId,
                        SubjectId = row.SubjectId,
                        TeacherId = teacherId,
                        AcademicYearId = academicYearId,
                        IsClassTeacher = isClassTeacher
                    },
                    cancellationToken).ConfigureAwait(false);

                affectedTeachers.Add(teacherId);
            }
        }

        foreach (Guid teacherId in affectedTeachers)
        {
            await BumpTeacherScopeIfLinkedAsync(teacherId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetBySubjectIdAsync(
        Guid subjectId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default)
    {
        Guid? yearId = academicYearId
            ?? await _scopeMapping.GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
                .ConfigureAwait(false);

        return await _repository.GetBySubjectIdAsync(subjectId, yearId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSubjectMappingsAsync(
        Guid subjectId,
        SaveSubjectMappingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Guid academicYearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ClassSubjectTeacherMappingDto> existing = await _repository
            .GetBySubjectIdAsync(subjectId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> affectedTeacherIds = existing.Select(e => e.TeacherId).ToHashSet();

        await _repository.SoftDeleteBySubjectAsync(subjectId, academicYearId, cancellationToken).ConfigureAwait(false);

        foreach (SubjectMappingGridRowDto row in request.Rows.Where(r => r.ClassId != Guid.Empty))
        {
            foreach (Guid teacherId in row.TeacherIds.Distinct().Where(id => id != Guid.Empty))
            {
                await _repository.InsertAsync(
                    new ClassSubjectTeacherMappingEntity
                    {
                        ClassId = row.ClassId,
                        SubjectId = subjectId,
                        TeacherId = teacherId,
                        AcademicYearId = academicYearId,
                        IsClassTeacher = false
                    },
                    cancellationToken).ConfigureAwait(false);

                affectedTeacherIds.Add(teacherId);
            }
        }

        foreach (Guid teacherId in affectedTeacherIds)
        {
            await BumpTeacherScopeIfLinkedAsync(teacherId, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SaveSubjectTeachersAsync(
        Guid subjectId,
        SaveSubjectTeachersRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Guid academicYearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ClassSubjectTeacherMappingDto> existing = await _repository
            .GetBySubjectIdAsync(subjectId, academicYearId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Guid> classIds = existing.Select(e => e.ClassId).Distinct().ToList();
        if (classIds.Count == 0)
        {
            classIds = await GetAllClassIdsForYearAsync(academicYearId, cancellationToken).ConfigureAwait(false);
        }

        List<SubjectMappingGridRowDto> rows = classIds
            .Select(classId => new SubjectMappingGridRowDto
            {
                ClassId = classId,
                TeacherIds = request.TeacherIds.Distinct().Where(id => id != Guid.Empty).ToList()
            })
            .Where(r => r.TeacherIds.Count > 0)
            .ToList();

        await SaveSubjectMappingsAsync(
            subjectId,
            new SaveSubjectMappingsRequestDto { AcademicYearId = academicYearId, Rows = rows },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClassSubjectTeacherMappingDto> AddMappingAsync(
        CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        Guid academicYearId = await ResolveAcademicYearIdAsync(request.AcademicYearId, cancellationToken)
            .ConfigureAwait(false);

        if (request.IsClassTeacher)
        {
            await _repository.ClearClassTeacherFlagAsync(request.ClassId, academicYearId, cancellationToken)
                .ConfigureAwait(false);
        }

        Guid id = await _repository.InsertAsync(
            new ClassSubjectTeacherMappingEntity
            {
                ClassId = request.ClassId,
                SubjectId = request.SubjectId,
                TeacherId = request.TeacherId,
                AcademicYearId = academicYearId,
                IsClassTeacher = request.IsClassTeacher
            },
            cancellationToken).ConfigureAwait(false);

        await BumpTeacherScopeIfLinkedAsync(request.TeacherId, cancellationToken).ConfigureAwait(false);

        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return new ClassSubjectTeacherMappingDto
        {
            Id = id,
            ClassId = request.ClassId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            AcademicYearId = academicYearId,
            IsClassTeacher = request.IsClassTeacher
        };
    }

    public async Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Mapping not found.");

        if (request.IsClassTeacher)
        {
            await _repository.ClearClassTeacherFlagAsync(entity.ClassId, entity.AcademicYearId, cancellationToken)
                .ConfigureAwait(false);
        }

        entity.IsClassTeacher = request.IsClassTeacher;
        entity.IsActive = true;
        await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        await BumpTeacherScopeIfLinkedAsync(entity.TeacherId, cancellationToken).ConfigureAwait(false);

        return new ClassSubjectTeacherMappingDto
        {
            Id = entity.Id,
            ClassId = entity.ClassId,
            SubjectId = entity.SubjectId,
            TeacherId = entity.TeacherId,
            AcademicYearId = entity.AcademicYearId,
            IsClassTeacher = entity.IsClassTeacher
        };
    }

    public async Task RemoveMappingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ClassSubjectTeacherMappingEntity? entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        await _repository.SoftDeleteAsync(id, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            await BumpTeacherScopeIfLinkedAsync(entity.TeacherId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<TeacherClassAssignmentRowDto> GroupRowsForTeacher(IReadOnlyList<ClassSubjectTeacherMappingDto> rows)
    {
        return rows
            .GroupBy(r => r.ClassId)
            .Select(g => new TeacherClassAssignmentRowDto
            {
                ClassId = g.Key,
                SubjectIds = g.Select(x => x.SubjectId).Distinct().ToList(),
                IsClassTeacher = g.Any(x => x.IsClassTeacher)
            })
            .ToList();
    }

    private async Task<Guid> ResolveAcademicYearIdAsync(Guid? academicYearId, CancellationToken cancellationToken)
    {
        if (academicYearId.HasValue)
        {
            return academicYearId.Value;
        }

        Guid? active = await _scopeMapping
            .GetActiveAcademicYearIdAsync(_context.OperationalSchema, cancellationToken)
            .ConfigureAwait(false);

        return active ?? throw new InvalidOperationException("No active academic year found.");
    }

    private async Task<IReadOnlyList<Guid>> GetAllClassIdsForYearAsync(Guid academicYearId, CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT id FROM {_context.OperationalSchema}.classes
WHERE academicyearid = @AcademicYearId AND isactive = true
""";
        using System.Data.IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: cancellationToken));
        return rows.ToList();
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
        using System.Data.IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
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
}
