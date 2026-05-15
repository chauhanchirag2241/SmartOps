using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Setting.Entities;

public class SettingEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
