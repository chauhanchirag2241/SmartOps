using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.FrontOffice;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

public sealed class AdmissionInquiryEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string ParentName { get; set; } = null!;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string StudentName { get; set; } = null!;
    public string? ClassLabel { get; set; }
    public DateOnly InquiryDate { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public Guid? AssignedToEmployeeId { get; set; }
    public string? Reference { get; set; }
    public InquiryStatus Status { get; set; }
    public string? Description { get; set; }
    public bool AutoFollowUp { get; set; }
    public short? StreamGroup { get; set; }
}
