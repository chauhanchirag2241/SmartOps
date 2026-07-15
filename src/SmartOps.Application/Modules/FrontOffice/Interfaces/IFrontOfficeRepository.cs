using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FrontOffice.Entities;

namespace SmartOps.Application.Modules.FrontOffice.Interfaces;

public interface IFrontOfficeRepository
{
    // Complaint types
    Task<IList<ComplaintTypeEntity>> GetComplaintTypesAsync(string? activeFilter = "All", CancellationToken ct = default);
    Task<ComplaintTypeEntity?> GetComplaintTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateComplaintTypeAsync(ComplaintTypeEntity entity, CancellationToken ct = default);
    Task UpdateComplaintTypeAsync(ComplaintTypeEntity entity, CancellationToken ct = default);
    Task SoftDeleteComplaintTypeAsync(Guid id, CancellationToken ct = default);

    // Visitor purposes
    Task<IList<VisitorPurposeEntity>> GetVisitorPurposesAsync(string? activeFilter = "All", CancellationToken ct = default);
    Task<VisitorPurposeEntity?> GetVisitorPurposeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateVisitorPurposeAsync(VisitorPurposeEntity entity, CancellationToken ct = default);
    Task UpdateVisitorPurposeAsync(VisitorPurposeEntity entity, CancellationToken ct = default);
    Task SoftDeleteVisitorPurposeAsync(Guid id, CancellationToken ct = default);

    // Visitors
    Task<IList<VisitorListRow>> GetVisitorsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);
    Task<VisitorListRow?> GetVisitorByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateVisitorAsync(VisitorEntity entity, CancellationToken ct = default);
    Task UpdateVisitorAsync(VisitorEntity entity, CancellationToken ct = default);
    Task SoftDeleteVisitorAsync(Guid id, CancellationToken ct = default);
    Task CheckoutVisitorAsync(Guid id, DateTimeOffset outTime, CancellationToken ct = default);

    // Phone logs
    Task<IList<PhoneLogEntity>> GetPhoneLogsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);
    Task<PhoneLogEntity?> GetPhoneLogByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreatePhoneLogAsync(PhoneLogEntity entity, CancellationToken ct = default);
    Task UpdatePhoneLogAsync(PhoneLogEntity entity, CancellationToken ct = default);
    Task SoftDeletePhoneLogAsync(Guid id, CancellationToken ct = default);

    // Complaints
    Task<IList<ComplaintListRow>> GetComplaintsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default);
    Task<ComplaintListRow?> GetComplaintByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateComplaintAsync(ComplaintEntity entity, CancellationToken ct = default);
    Task UpdateComplaintAsync(ComplaintEntity entity, CancellationToken ct = default);
    Task SoftDeleteComplaintAsync(Guid id, CancellationToken ct = default);

    // Admission inquiries
    Task<IList<AdmissionInquiryListRow>> GetAdmissionInquiriesAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default);
    Task<AdmissionInquiryListRow?> GetAdmissionInquiryByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAdmissionInquiryAsync(AdmissionInquiryEntity entity, CancellationToken ct = default);
    Task UpdateAdmissionInquiryAsync(AdmissionInquiryEntity entity, CancellationToken ct = default);
    Task SoftDeleteAdmissionInquiryAsync(Guid id, CancellationToken ct = default);
    Task ConvertAdmissionInquiryAsync(Guid id, CancellationToken ct = default);

    // Lookups
    Task<IReadOnlyList<DropdownDto>> GetActiveEmployeesAsync(CancellationToken ct = default);
}

public sealed class VisitorListRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? IdCardType { get; set; }
    public string? IdCardNumber { get; set; }
    public Guid PurposeId { get; set; }
    public string? PurposeName { get; set; }
    public string? MeetingWith { get; set; }
    public DateTimeOffset InTime { get; set; }
    public DateTimeOffset? OutTime { get; set; }
    public string? Note { get; set; }
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ComplaintListRow
{
    public Guid Id { get; set; }
    public Guid ComplaintTypeId { get; set; }
    public string? ComplaintTypeName { get; set; }
    public DateOnly ComplaintDate { get; set; }
    public bool IsAnonymous { get; set; }
    public string? ComplainantName { get; set; }
    public string? Phone { get; set; }
    public string Description { get; set; } = null!;
    public Guid AssignedToEmployeeId { get; set; }
    public string? AssignedToEmployeeName { get; set; }
    public short Status { get; set; }
    public string? ActionTaken { get; set; }
    public string? Note { get; set; }
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdmissionInquiryListRow
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
    public string? AssignedToEmployeeName { get; set; }
    public string? Reference { get; set; }
    public short Status { get; set; }
    public string? Description { get; set; }
    public bool AutoFollowUp { get; set; }
    public short? StreamGroup { get; set; }
    public bool IsActive { get; set; }
}
