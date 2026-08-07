using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Employee.Entities;

[TrackHistory]
public class EmployeeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Profile fields live on users; kept for API transport only.</summary>
    [DbIgnore] [TrackHistoryIgnore] public string FirstName { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string LastName { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string Mobile { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string Email { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string? Username { get; set; }
    [DbIgnore] [TrackHistoryIgnore] public string UserTypeCode { get; set; } = "Teacher";

    public DateOnly Dob { get; set; }
    public string Gender { get; set; } = null!;
    public string? BloodGroup { get; set; }
    public string? AadhaarNo { get; set; }
    public string? PanNo { get; set; }
    public string? AlternateMobile { get; set; }
    public string? Address { get; set; }
    public string? EmployeeCode { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string? Designation { get; set; }
    public int Experience { get; set; }
    public string? Qualifications { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfscCode { get; set; }
    public string? BankName { get; set; }
    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }

    /// <summary>Mapped shift master IDs; transport only — stored in employeeshifts. Null on update = leave mappings unchanged.</summary>
    [DbIgnore]
    [TrackHistoryIgnore]
    public List<Guid>? ShiftIds { get; set; }

    public string PortalRoleName { get; set; } = "Teacher";
    public bool PortalAccess { get; set; } = true;
    public Guid? DepartmentId { get; set; }
    public Guid? ReportingManagerId { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
