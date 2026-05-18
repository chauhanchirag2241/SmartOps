namespace SmartOps.Application.Modules.Teacher.DTOs;

public sealed class TeacherClassAssignmentRowDto
{
    public Guid ClassId { get; set; }

    public List<Guid> SubjectIds { get; set; } = [];

    public bool IsClassTeacher { get; set; }

    public bool CanViewStudents { get; set; } = true;

    public bool CanMarkAttendance { get; set; }

    public bool CanAddMarks { get; set; }

    public bool CanSendNotice { get; set; }
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
