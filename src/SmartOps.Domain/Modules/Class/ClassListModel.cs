namespace SmartOps.Domain.Modules.Class;

/// <summary>
/// Flat projection returned by the paged list query.
/// </summary>
public class ClassListModel
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public string ClassName { get; set; } = null!;
    public string Section { get; set; } = null!;
    public int Capacity { get; set; }
    public string? RoomNumber { get; set; }
    public string Status { get; set; } = null!;
    public bool IsActive { get; set; }
}
