using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Notice;

namespace SmartOps.Domain.Modules.Notice.Entities;

public sealed class NoticeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset? PublishedOn { get; set; }
    public bool RequiresResponse { get; set; }
    public DateOnly? ResponseDeadline { get; set; }
    public NoticeTargetType TargetType { get; set; }
    public Guid? TargetRefId { get; set; }
    public NoticeContentType ContentType { get; set; } = NoticeContentType.Announcement;
    public string? ContentJson { get; set; }
    public NoticeStatus Status { get; set; }
}
