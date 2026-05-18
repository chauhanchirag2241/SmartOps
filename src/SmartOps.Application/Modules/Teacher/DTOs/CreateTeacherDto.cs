using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Teacher.DTOs;

public class CreateTeacherDto
{
    public TeacherPersonalInfo Personal { get; set; } = null!;
    public TeacherProfessionalInfo Professional { get; set; } = null!;
    public TeacherScheduleInfo Schedule { get; set; } = null!;

    public TeacherEntity ToEntity()
    {
        return new TeacherEntity
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
            // Professional
            JoiningDate = Professional.JoiningDate,
            Department = Professional.Department,
            Designation = Professional.Designation,
            Experience = Professional.Experience,
            SalaryGrade = Professional.SalaryGrade,
            EmploymentType = Professional.EmploymentType,
            Qualifications = Professional.Qualifications != null ? string.Join("; ", Professional.Qualifications) : null,
            BankAccountNumber = Professional.BankDetails?.AccountNumber,
            BankIfscCode = Professional.BankDetails?.IfscCode,
            BankName = Professional.BankDetails?.BankName,
            // Schedule
            ClassId = Schedule.ClassId,
            Shift = Schedule.Shift,
            WeeklyPeriods = Schedule.WeeklyPeriods,
            MaxPeriodsPerDay = Schedule.MaxPeriodsPerDay,
            Role = Schedule.Role,
            PortalAccess = Schedule.PortalAccess == "Enabled",
            Username = Schedule.Username,
            IsActive = true
        };
    }
}

public class TeacherPersonalInfo
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

public class TeacherProfessionalInfo
{
    public DateOnly JoiningDate { get; set; }
    public string Department { get; set; } = null!;
    public string Designation { get; set; } = null!;
    public int Experience { get; set; }
    public string? SalaryGrade { get; set; }
    public string EmploymentType { get; set; } = "Full-time";
    public List<string>? Qualifications { get; set; }
    public TeacherBankDetails? BankDetails { get; set; }
}

public class TeacherBankDetails
{
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? BankName { get; set; }
}

public class TeacherScheduleInfo
{
    public Guid? ClassId { get; set; }

    public List<TeacherClassAssignmentRowDto> ClassAssignments { get; set; } = [];

    public string? Shift { get; set; }
    public int WeeklyPeriods { get; set; }
    public int MaxPeriodsPerDay { get; set; }
    public string Role { get; set; } = "Teacher";
    public string PortalAccess { get; set; } = "Enabled";
    public string? Username { get; set; }
}

public sealed record CreateTeacherResponse(string Message, Guid TeacherId);
