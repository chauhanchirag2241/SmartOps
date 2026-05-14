namespace SmartOps.Domain.Modules.Teacher.Models;

public class TeacherListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Dept { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public bool IsActive { get; set; }
}
