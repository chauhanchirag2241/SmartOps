using System.Text.Json.Serialization;
using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Application.Modules.Student;

public class CreateStudentDto
{
    // Personal Info
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
    public string? Caste { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Remarks { get; set; }
    public bool PortalAccess { get; set; }

    // Navigation
    public List<CreateStudentParentDto> Parents { get; set; } = new();
    public List<CreateStudentAcademicDto> Academics { get; set; } = new();
    public List<CreateStudentPreviousSchoolDto> PreviousSchools { get; set; } = new();
    public List<CreateStudentFeeConfigDto> FeeConfigs { get; set; } = new();
    public List<StudentCustomFieldDto> CustomFields { get; set; } = new();
}

public class StudentCustomFieldDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = null!;

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class CreateStudentParentDto
{
    public string RelationType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Mobile { get; set; }
    public string? Occupation { get; set; }
}

public class CreateStudentAcademicDto
{
    public DateOnly? AdmissionDate { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public string? RollNumber { get; set; }
}

public class CreateStudentPreviousSchoolDto
{
    public string? SchoolName { get; set; }
    public string? LastClassPassed { get; set; }
    public string? PercentageOrCgpa { get; set; }
    public string? TcNumber { get; set; }
}

public class CreateStudentFeeConfigDto
{
    public string? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public bool? IsPercentage { get; set; }
    public string? DiscountRemarks { get; set; }
    public string? PaymentMode { get; set; }
    public DateOnly? FirstDueDate { get; set; }
}

public static class StudentMappingExtensions
{
    public static StudentEntity ToEntity(this CreateStudentDto dto)
    {
        return new StudentEntity
        {
            AdmissionNo = dto.AdmissionNo,
            FirstName = dto.FirstName,
            MiddleName = dto.MiddleName,
            LastName = dto.LastName,
            Dob = dto.Dob,
            Gender = dto.Gender,
            BloodGroup = dto.BloodGroup,
            Mobile = dto.Mobile,
            Email = dto.Email,
            AadhaarNo = dto.AadhaarNo,
            Caste = dto.Caste,
            Category = dto.Category,
            Address = dto.Address,
            PhotoUrl = dto.PhotoUrl,
            Remarks = dto.Remarks,
            PortalAccess = dto.PortalAccess,
            //Status = "Active",
            Parents = dto.Parents.Select(p => new StudentParentEntity
            {
                RelationType = p.RelationType,
                Name = p.Name,
                Mobile = p.Mobile,
                Occupation = p.Occupation
            }).ToList(),
            Academics = dto.Academics.Select(a => new StudentAcademicEntity
            {
                AdmissionDate = a.AdmissionDate,
                AcademicYearId = a.AcademicYearId,
                ClassId = a.ClassId,
                RollNumber = a.RollNumber
            }).ToList(),
            PreviousSchools = dto.PreviousSchools.Select(ps => new StudentPreviousSchoolEntity
            {
                SchoolName = ps.SchoolName,
                LastClassPassed = ps.LastClassPassed,
                PercentageOrCgpa = ps.PercentageOrCgpa,
                TcNumber = ps.TcNumber
            }).ToList(),
            FeeConfigs = dto.FeeConfigs.Select(f => new StudentFeeConfigEntity
            {
                DiscountType = f.DiscountType,
                DiscountValue = f.DiscountValue,
                IsPercentage = f.IsPercentage,
                DiscountRemarks = f.DiscountRemarks,
                PaymentMode = f.PaymentMode,
                FirstDueDate = f.FirstDueDate
            }).ToList(),
            CustomFields = dto.CustomFields
                .Where(cf => !string.IsNullOrWhiteSpace(cf.Label) || !string.IsNullOrWhiteSpace(cf.Value))
                .Select(cf => new StudentCustomFieldEntity
                {
                    FieldLabel = cf.Label.Trim(),
                    FieldValue = string.IsNullOrWhiteSpace(cf.Value) ? null : cf.Value.Trim()
                })
                .ToList()
        };
    }
}

/// <summary>Standard API payload after creating a student record.</summary>
public sealed record CreateStudentResponse(string Message, Guid StudentId);
