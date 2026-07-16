using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Employee.Entities;

[TrackHistory]
public class EmployeeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateOnly Dob { get; set; }
    public string Gender { get; set; } = null!;
    public string? BloodGroup { get; set; }
    public string? AadhaarNo { get; set; }
    public string? PanNo { get; set; }
    public string Mobile { get; set; } = null!;
    public string? AlternateMobile { get; set; }
    public string Email { get; set; } = null!;
    public string? Address { get; set; }
    public string? EmployeeId { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string? Designation { get; set; }
    public int Experience { get; set; }
    public string? SalaryGrade { get; set; }
    public string EmploymentType { get; set; } = "Full-time";
    public string? Qualifications { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfscCode { get; set; }
    public string? BankName { get; set; }
    public Guid? ClassId { get; set; }
    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }
    public string UserTypeCode { get; set; } = "TEACHER";
    public string PortalRoleName { get; set; } = "Teacher";
    public bool PortalAccess { get; set; } = true;
    public string? Username { get; set; }
    public Guid? UserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? ReportingManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
