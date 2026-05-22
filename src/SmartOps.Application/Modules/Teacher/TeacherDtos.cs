using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Teacher;

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
            Designation = string.IsNullOrWhiteSpace(Professional.Designation) ? null : Professional.Designation.Trim(),
            Experience = Professional.Experience,
            SalaryGrade = Professional.SalaryGrade,
            EmploymentType = Professional.EmploymentType,
            Qualifications = Professional.Qualifications != null ? string.Join("; ", Professional.Qualifications) : null,
            BankAccountNumber = Professional.BankDetails?.AccountNumber,
            BankIfscCode = Professional.BankDetails?.IfscCode,
            BankName = Professional.BankDetails?.BankName,
            // Schedule
            ClassId = Schedule.ClassId,
            ShiftStartTime = Schedule.ShiftStartTime,
            ShiftEndTime = Schedule.ShiftEndTime,
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
    public string? Designation { get; set; }
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

    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }
    public int? WeeklyPeriods { get; set; }
    public int? MaxPeriodsPerDay { get; set; }
    public string Role { get; set; } = "Teacher";
    public string PortalAccess { get; set; } = "Enabled";
    public string? Username { get; set; }
}

public sealed record CreateTeacherResponse(string Message, Guid TeacherId);

public sealed class ClassSubjectTeacherMappingDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string? ClassName { get; set; }

    public Guid SubjectId { get; set; }

    public string? SubjectName { get; set; }

    public string? SubjectCode { get; set; }

    public Guid? TeacherId { get; set; }

    public string? TeacherName { get; set; }

    public Guid AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; }
}

public sealed class MappingLookupOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? SubLabel { get; set; }
}

public sealed class ClassMappingSummaryDto
{
    public Guid ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public string? Section { get; set; }

    public int SubjectCount { get; set; }

    public int TeachersAssignedCount { get; set; }

    public int ClassTeacherCount { get; set; }
}

public sealed class MappingLookupsResponseDto
{
    public Guid? ActiveAcademicYearId { get; set; }

    public IReadOnlyList<MappingLookupOptionDto> AcademicYears { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Classes { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Subjects { get; set; } = [];

    public IReadOnlyList<MappingLookupOptionDto> Teachers { get; set; } = [];

    public IReadOnlyList<ClassMappingSummaryDto> ClassSummaries { get; set; } = [];
}

/// <summary>Legacy shape retained for teacher create payload compatibility.</summary>
public sealed class TeacherClassAssignmentRowDto
{
    public Guid ClassId { get; set; }

    public List<Guid> SubjectIds { get; set; } = [];

    public bool IsClassTeacher { get; set; }
}

/// <summary>Reserved for future student-to-class mapping rows.</summary>
public sealed class StudentMappingPlaceholderDto
{
    public Guid StudentId { get; set; }

    public Guid ClassId { get; set; }
}

public sealed class CreateClassSubjectTeacherMappingDto
{
    public Guid ClassId { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary>Null or empty GUID means assign teacher later.</summary>
    public Guid? TeacherId { get; set; }

    public Guid? AcademicYearId { get; set; }

    public bool IsClassTeacher { get; set; }
}

public sealed class UpdateClassSubjectTeacherMappingDto
{
    public Guid? TeacherId { get; set; }

    public bool? IsClassTeacher { get; set; }

    public bool AssignLater { get; set; }
}

public sealed class AssignTeacherLaterRequestDto
{
    /// <summary>When true, clears teacher assignment (assign later).</summary>
    public bool AssignLater { get; set; }

    public Guid? TeacherId { get; set; }
}

public sealed class SetClassTeacherRequestDto
{
    public bool IsClassTeacher { get; set; }
}

