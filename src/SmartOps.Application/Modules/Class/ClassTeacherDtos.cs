namespace SmartOps.Application.Modules.Class;

public sealed class ClassTeacherAssignmentDto
{
    public Guid Id { get; set; }

    /// <summary>Section / class id (<c>classes.id</c>).</summary>
    public Guid ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public Guid? ClassGroupId { get; set; }

    public Guid TeacherId { get; set; }
}

public sealed class AssignClassTeacherRequestDto
{
    public Guid EmployeeId { get; set; }

    /// <summary>Section / class id to assign as class teacher.</summary>
    public Guid ClassId { get; set; }
}
