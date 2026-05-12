using System;

namespace SmartOps.Domain.Modules.Student.Models;

public class StudentListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string AdmNo { get; set; } = null!;
    public string Class { get; set; } = null!;
    public int Attendance { get; set; }
    public string Fees { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
