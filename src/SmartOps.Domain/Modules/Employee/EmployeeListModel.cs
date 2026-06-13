namespace SmartOps.Domain.Modules.Employee;

public class EmployeeListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Designation { get; set; }
    public string UserTypeCode { get; set; } = null!;
    public string? DepartmentName { get; set; }
    public string? ReportingManagerName { get; set; }
    public bool IsActive { get; set; }
}
