namespace SmartOps.Domain.Modules.Timetable;

public sealed class PeriodListModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int PeriodOrder { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public bool IsBreak { get; set; }
    public bool IsActive { get; set; }
}
