namespace SmartOps.Domain.Modules.Timetable;

public sealed class PeriodTemplateListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PeriodCount { get; set; }
    public int TeachingPeriodCount { get; set; }
    public bool IsActive { get; set; }
}
