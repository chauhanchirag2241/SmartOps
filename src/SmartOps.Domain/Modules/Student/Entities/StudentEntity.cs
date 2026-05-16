using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Student.Entities;

public class StudentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string? AdmissionNo { get; set; }
    public string FirstName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = null!;
    public DateOnly? Dob { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? AadhaarNo { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    //public string Status { get; set; } = "Active";
    public string? Remarks { get; set; }
    public Guid? UserId { get; set; }
    public bool PortalAccess { get; set; }

    // Navigation Properties
    [DbIgnore] public List<StudentParentEntity> Parents { get; set; } = new();
    [DbIgnore] public List<StudentAcademicEntity> Academics { get; set; } = new();
    [DbIgnore] public List<StudentPreviousSchoolEntity> PreviousSchools { get; set; } = new();
    [DbIgnore] public List<StudentFeeConfigEntity> FeeConfigs { get; set; } = new();
}
