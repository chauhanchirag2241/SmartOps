namespace SmartOps.Domain.Modules.Shift;

public sealed class ShiftListModel
{
    public Guid Id { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
