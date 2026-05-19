namespace SmartOps.Application.Modules.Teacher.DTOs;

public sealed class ClassSubjectTeacherMappingDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string? ClassName { get; set; }

    public Guid SubjectId { get; set; }

    public string? SubjectName { get; set; }

    public Guid TeacherId { get; set; }

    public string? TeacherName { get; set; }

    public Guid AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; }
}

public sealed class TeacherClassAssignmentRowDto
{
    public Guid ClassId { get; set; }

    public List<Guid> SubjectIds { get; set; } = [];

    public bool IsClassTeacher { get; set; }
}

public sealed class TeacherAssignmentsResponseDto
{
    public Guid TeacherId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public List<TeacherClassAssignmentRowDto> ClassAssignments { get; set; } = [];
}

public sealed class SaveTeacherAssignmentsRequestDto
{
    public Guid? AcademicYearId { get; set; }

    public List<TeacherClassAssignmentRowDto> ClassAssignments { get; set; } = [];
}

public sealed class ClassMappingGridRowDto
{
    public Guid SubjectId { get; set; }

    public string? SubjectName { get; set; }

    public List<Guid> TeacherIds { get; set; } = [];
}

public sealed class SaveClassMappingsRequestDto
{
    public Guid? AcademicYearId { get; set; }

    public Guid? ClassTeacherId { get; set; }

    public List<ClassMappingGridRowDto> Rows { get; set; } = [];
}

public sealed class SubjectMappingGridRowDto
{
    public Guid ClassId { get; set; }

    public string? ClassName { get; set; }

    public List<Guid> TeacherIds { get; set; } = [];
}

public sealed class SaveSubjectMappingsRequestDto
{
    public Guid? AcademicYearId { get; set; }

    public List<SubjectMappingGridRowDto> Rows { get; set; } = [];
}

/// <summary>Legacy flat teacher list — prefer <see cref="SaveSubjectMappingsRequestDto"/>.</summary>
public sealed class SaveSubjectTeachersRequestDto
{
    public Guid? AcademicYearId { get; set; }

    public List<Guid> TeacherIds { get; set; } = [];
}

public sealed class CreateClassSubjectTeacherMappingDto
{
    public Guid ClassId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; }
}

public sealed class UpdateClassSubjectTeacherMappingDto
{
    public bool IsClassTeacher { get; set; }
}
