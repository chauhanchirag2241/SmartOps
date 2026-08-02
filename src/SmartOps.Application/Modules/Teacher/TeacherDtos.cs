namespace SmartOps.Application.Modules.Teacher;

public sealed class ClassSubjectTeacherMappingDto
{
    public Guid Id { get; set; }

    public Guid ClassGroupId { get; set; }

    public string ClassGroupName { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public string? SubjectCode { get; set; }

    public Guid EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public Guid AcademicYearId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CreateClassSubjectTeacherMappingDto
{
    public Guid ClassGroupId { get; set; }

    /// <summary>Preferred: single subject for one mapping row.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Optional convenience: expands to one row per subject (same as bulk item).</summary>
    public List<Guid> SubjectIds { get; set; } = [];

    public Guid EmployeeId { get; set; }

    public Guid AcademicYearId { get; set; }
}

public sealed class UpdateClassSubjectTeacherMappingDto
{
    /// <summary>Optional: change the subject on this mapping row.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Optional: reactivate (true) or soft-deactivate (false). Prefer SoftDelete endpoint for remove.</summary>
    public bool? IsActive { get; set; }
}

public sealed class BulkClassSubjectTeacherMappingItemDto
{
    public Guid ClassGroupId { get; set; }

    /// <summary>API expands each subject into its own mapping row.</summary>
    public List<Guid> SubjectIds { get; set; } = [];
}

public sealed class BulkCreateClassSubjectTeacherMappingsRequestDto
{
    public Guid EmployeeId { get; set; }

    public Guid AcademicYearId { get; set; }

    public IReadOnlyList<BulkClassSubjectTeacherMappingItemDto> Mappings { get; set; } = [];

    /// <summary>Optional section (class) ids to mark this employee as class teacher via classsettings.</summary>
    public IReadOnlyList<Guid>? ClassTeacherClassIds { get; set; }
}

public sealed class BulkCreateClassSubjectTeacherMappingsResultDto
{
    public int CreatedCount { get; set; }

    public IReadOnlyList<ClassSubjectTeacherMappingDto> Created { get; set; } = [];
}

public sealed class MappingLookupOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? SubLabel { get; set; }
}

public sealed class ClassMappingSummaryDto
{
    public Guid ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string? Section { get; set; }

    public int SubjectCount { get; set; }

    public int EmployeesAssignedCount { get; set; }

    public int ClassTeacherCount { get; set; }
}

public sealed class MappingLookupsResponseDto
{
    public Guid ActiveAcademicYearId { get; set; }

    public IReadOnlyList<MappingLookupOptionDto> AcademicYears { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Classes { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Subjects { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Employees { get; set; } = [];

    public IReadOnlyList<ClassMappingSummaryDto> ClassSummaries { get; set; } = [];
}
