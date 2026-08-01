namespace SmartOps.Application.Modules.Teacher;

public sealed class ClassSubjectTeacherMappingDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string SubjectName { get; set; } = string.Empty;

    public string? SubjectCode { get; set; }

    public Guid? EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Derived from classsettings for the mapping's class — not stored on the mapping row.</summary>
    public bool IsClassTeacher { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CreateClassSubjectTeacherMappingDto
{
    public Guid ClassId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    public Guid AcademicYearId { get; set; }
}

public sealed class UpdateClassSubjectTeacherMappingDto
{
    public Guid? SubjectId { get; set; }

    public Guid? EmployeeId { get; set; }

    /// <summary>When true, clears the teacher assignment (assign later).</summary>
    public bool AssignLater { get; set; }
}

public sealed class AssignTeacherLaterRequestDto
{
    public bool AssignLater { get; set; } = true;

    public Guid? EmployeeId { get; set; }
}

public sealed class SetClassTeacherRequestDto
{
    public bool IsClassTeacher { get; set; } = true;
}

public sealed class BulkCreateClassSubjectTeacherMappingsRequestDto
{
    public Guid EmployeeId { get; set; }

    public Guid AcademicYearId { get; set; }

    public IReadOnlyList<CreateClassSubjectTeacherMappingDto> Mappings { get; set; } = [];

    /// <summary>Section (class) ids where this employee should be the class teacher.</summary>
    public IReadOnlyList<Guid> ClassTeacherClassIds { get; set; } = [];
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
