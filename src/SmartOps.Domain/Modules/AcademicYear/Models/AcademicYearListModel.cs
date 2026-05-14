namespace SmartOps.Domain.Modules.AcademicYear.Models;

public class AcademicYearListModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
