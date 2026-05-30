using System;

namespace SmartOps.Domain.Modules.Student;

public class StudentListModel
{
    public Guid Id { get; set; }
    public Guid? ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string AdmNo { get; set; } = null!;
    public string? RollNumber { get; set; }
    public string Class { get; set; } = null!;
    public int Attendance { get; set; }
    public string Fees { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
    /// <summary>True when the student has an active class enrollment in the scoped academic year.</summary>
    public bool EnrollmentIsActive { get; set; }
}
