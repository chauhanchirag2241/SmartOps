using SmartOps.Domain.Modules.Employee.Entities;

namespace SmartOps.Application.Modules.Employee;

public class CreateEmployeeDto
{
    public EmployeePersonalInfo Personal { get; set; } = null!;
    public EmployeeProfessionalInfo Professional { get; set; } = null!;
    public EmployeeAccessInfo Access { get; set; } = null!;
    public EmployeeOrganizationInfo Organization { get; set; } = null!;
    public EmployeeScheduleInfo Schedule { get; set; } = null!;

    public EmployeeEntity ToEntity()
    {
        var entity = new EmployeeEntity
        {
            FirstName = Personal.FirstName,
            LastName = Personal.LastName,
            Dob = Personal.Dob,
            BloodGroup = Personal.BloodGroup,
            Gender = Personal.Gender,
            AadhaarNo = Personal.AadhaarNumber,
            PanNo = Personal.PanNumber,
            Mobile = Personal.Mobile,
            AlternateMobile = Personal.AlternateMobile,
            Email = Personal.Email,
            Address = Personal.Address,
            JoiningDate = Professional.JoiningDate,
            Designation = string.IsNullOrWhiteSpace(Professional.Designation) ? null : Professional.Designation.Trim(),
            Experience = Professional.Experience,
            SalaryGrade = Professional.SalaryGrade,
            EmploymentType = Professional.EmploymentType,
            Qualifications = Professional.Qualifications != null ? string.Join("; ", Professional.Qualifications) : null,
            BankAccountNumber = Professional.BankDetails?.AccountNumber,
            BankIfscCode = Professional.BankDetails?.IfscCode,
            BankName = Professional.BankDetails?.BankName,
            UserTypeCode = Access.UserTypeCode,
            PortalRoleName = Access.PortalRoleName,
            PortalAccess = Access.PortalAccess == "Enabled",
            Username = Access.Username,
            DepartmentId = Organization.DepartmentId,
            ReportingManagerId = Organization.ReportingManagerId,
            ClassId = Schedule.ClassId,
            ShiftStartTime = Schedule.ShiftStartTime,
            ShiftEndTime = Schedule.ShiftEndTime,
            IsActive = true
        };

        ApplyEmployeeTypeRules(entity);
        return entity;
    }

    private static void ApplyEmployeeTypeRules(EmployeeEntity entity)
    {
        if (entity.ReportingManagerId == entity.Id)
        {
            entity.ReportingManagerId = null;
        }

        if (entity.UserTypeCode is not "TEACHER" and not "HOD")
        {
            entity.ClassId = null;
        }
    }
}

public class EmployeePersonalInfo
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateOnly Dob { get; set; }
    public string? BloodGroup { get; set; }
    public string Gender { get; set; } = null!;
    public string? AadhaarNumber { get; set; }
    public string? PanNumber { get; set; }
    public string Mobile { get; set; } = null!;
    public string? AlternateMobile { get; set; }
    public string Email { get; set; } = null!;
    public string? Address { get; set; }
}

public class EmployeeProfessionalInfo
{
    public DateOnly JoiningDate { get; set; }
    public string? Designation { get; set; }
    public int Experience { get; set; }
    public string? SalaryGrade { get; set; }
    public string EmploymentType { get; set; } = "Full-time";
    public List<string>? Qualifications { get; set; }
    public EmployeeBankDetails? BankDetails { get; set; }
}

public class EmployeeBankDetails
{
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? BankName { get; set; }
}

public class EmployeeAccessInfo
{
    public string UserTypeCode { get; set; } = "TEACHER";
    public string PortalRoleName { get; set; } = "Teacher";
    public string PortalAccess { get; set; } = "Enabled";
    public string? Username { get; set; }
}

public class EmployeeOrganizationInfo
{
    public Guid? DepartmentId { get; set; }
    public Guid? ReportingManagerId { get; set; }
}

public class EmployeeScheduleInfo
{
    public Guid? ClassId { get; set; }
    public List<EmployeeClassAssignmentRowDto> ClassAssignments { get; set; } = [];
    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }
}

public sealed record CreateEmployeeResponse(string Message, Guid EmployeeId);

public sealed class EmployeeClassAssignmentRowDto
{
    public Guid ClassId { get; set; }
    public List<Guid> SubjectIds { get; set; } = [];
    public bool IsClassTeacher { get; set; }
}
