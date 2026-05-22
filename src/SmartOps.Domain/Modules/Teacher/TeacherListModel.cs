namespace SmartOps.Domain.Modules.Teacher;

public class TeacherListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Designation { get; set; }
    public bool IsActive { get; set; }
}
