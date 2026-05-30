namespace SmartOps.Domain.Modules.AcademicYear;

public sealed class AcademicYearDropdownItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}
