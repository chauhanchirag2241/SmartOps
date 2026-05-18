namespace SmartOps.Application.Modules.Authorization.DTOs;

public sealed class AssignTeacherClassDto
{
    public Guid TeacherId { get; set; }

    public Guid ClassId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; } = true;
}

public sealed class AssignParentStudentDto
{
    public string ParentEmail { get; set; } = null!;

    public string? ParentUsername { get; set; }

    public Guid StudentId { get; set; }

    public string RelationType { get; set; } = "Parent";
}

public sealed class AssignHodDepartmentDto
{
    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid? AcademicYearId { get; set; }
}
