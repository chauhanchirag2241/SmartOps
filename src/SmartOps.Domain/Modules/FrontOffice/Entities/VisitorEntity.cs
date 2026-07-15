using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

public sealed class VisitorEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? IdCardType { get; set; }
    public string? IdCardNumber { get; set; }
    public Guid PurposeId { get; set; }
    public string? MeetingWith { get; set; }
    public DateTimeOffset InTime { get; set; }
    public DateTimeOffset? OutTime { get; set; }
    public string? Note { get; set; }
    public string? DocumentPath { get; set; }
}
