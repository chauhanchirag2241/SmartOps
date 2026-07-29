using System.Text.Json.Serialization;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Student.Entities;

[TrackHistory]
public class StudentEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid UserId { get; set; }
    public string? AdmissionNo { get; set; }
    public string? MiddleName { get; set; }

    /// <summary>Profile fields live on users; kept for API transport only.</summary>
    [DbIgnore] [TrackHistoryIgnore] public string FirstName { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string LastName { get; set; } = null!;
    [DbIgnore] [TrackHistoryIgnore] public string? Mobile { get; set; }
    [DbIgnore] [TrackHistoryIgnore] public string? Email { get; set; }

    public DateOnly? Dob { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public string? AadhaarNo { get; set; }
    public string? Caste { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Remarks { get; set; }
    public bool PortalAccess { get; set; } = true;

    [DbIgnore] [TrackHistoryIgnore] public List<StudentParentEntity> Parents { get; set; } = new();
    [DbIgnore] [TrackHistoryIgnore] public List<StudentAcademicEntity> Academics { get; set; } = new();
    [DbIgnore] [TrackHistoryIgnore] public List<StudentPreviousSchoolEntity> PreviousSchools { get; set; } = new();
    [DbIgnore] [TrackHistoryIgnore] public List<StudentFeeHeadAssignmentEntity> FeeHeadAssignments { get; set; } = new();
    [DbIgnore]
    [TrackHistoryIgnore]
    [JsonPropertyName("customFields")]
    public List<StudentCustomFieldEntity> CustomFields { get; set; } = new();

    [DbIgnore] [TrackHistoryIgnore] public List<StudentDocumentEntity> Documents { get; set; } = new();
}
