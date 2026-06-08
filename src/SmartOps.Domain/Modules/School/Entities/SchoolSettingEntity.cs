using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class SchoolSettingEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;
}
