using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Jobs.Entities;

public sealed class JobDefinitionEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CronExpression { get; set; } = "0 0 1 * *";
    public string TimeZoneId { get; set; } = "India Standard Time";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class HangfireConfigEntity
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedOn { get; set; }
}
