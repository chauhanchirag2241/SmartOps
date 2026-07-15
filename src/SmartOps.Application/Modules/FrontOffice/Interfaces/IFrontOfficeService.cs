using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Models;

namespace SmartOps.Application.Modules.FrontOffice.Interfaces;

public interface IFrontOfficeService
{
    // Complaint types
    Task<Result<IList<ComplaintTypeDto>>> GetComplaintTypesAsync(string? activeFilter = "All", CancellationToken ct = default);
    Task<Result<ComplaintTypeDto>> GetComplaintTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ComplaintTypeDto>> CreateComplaintTypeAsync(CreateComplaintTypeRequestDto request, CancellationToken ct = default);
    Task<Result<ComplaintTypeDto>> UpdateComplaintTypeAsync(Guid id, UpdateComplaintTypeRequestDto request, CancellationToken ct = default);
    Task<Result> DeleteComplaintTypeAsync(Guid id, CancellationToken ct = default);

    // Visitor purposes
    Task<Result<IList<VisitorPurposeDto>>> GetVisitorPurposesAsync(string? activeFilter = "All", CancellationToken ct = default);
    Task<Result<VisitorPurposeDto>> GetVisitorPurposeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<VisitorPurposeDto>> CreateVisitorPurposeAsync(CreateVisitorPurposeRequestDto request, CancellationToken ct = default);
    Task<Result<VisitorPurposeDto>> UpdateVisitorPurposeAsync(Guid id, UpdateVisitorPurposeRequestDto request, CancellationToken ct = default);
    Task<Result> DeleteVisitorPurposeAsync(Guid id, CancellationToken ct = default);

    // Visitors
    Task<Result<IList<VisitorDto>>> GetVisitorsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);
    Task<Result<VisitorDto>> GetVisitorByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<VisitorDto>> CreateVisitorAsync(CreateVisitorRequestDto request, CancellationToken ct = default);
    Task<Result<VisitorDto>> UpdateVisitorAsync(Guid id, UpdateVisitorRequestDto request, CancellationToken ct = default);
    Task<Result> DeleteVisitorAsync(Guid id, CancellationToken ct = default);
    Task<Result<VisitorDto>> CheckoutVisitorAsync(Guid id, CancellationToken ct = default);

    // Phone logs
    Task<Result<IList<PhoneLogDto>>> GetPhoneLogsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);
    Task<Result<PhoneLogDto>> GetPhoneLogByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PhoneLogDto>> CreatePhoneLogAsync(CreatePhoneLogRequestDto request, CancellationToken ct = default);
    Task<Result<PhoneLogDto>> UpdatePhoneLogAsync(Guid id, UpdatePhoneLogRequestDto request, CancellationToken ct = default);
    Task<Result> DeletePhoneLogAsync(Guid id, CancellationToken ct = default);

    // Complaints
    Task<Result<IList<ComplaintDto>>> GetComplaintsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default);
    Task<Result<ComplaintDto>> GetComplaintByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ComplaintDto>> CreateComplaintAsync(CreateComplaintRequestDto request, CancellationToken ct = default);
    Task<Result<ComplaintDto>> UpdateComplaintAsync(Guid id, UpdateComplaintRequestDto request, CancellationToken ct = default);
    Task<Result> DeleteComplaintAsync(Guid id, CancellationToken ct = default);

    // Admission inquiries
    Task<Result<IList<AdmissionInquiryDto>>> GetAdmissionInquiriesAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default);
    Task<Result<AdmissionInquiryDto>> GetAdmissionInquiryByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AdmissionInquiryDto>> CreateAdmissionInquiryAsync(CreateAdmissionInquiryRequestDto request, CancellationToken ct = default);
    Task<Result<AdmissionInquiryDto>> UpdateAdmissionInquiryAsync(Guid id, UpdateAdmissionInquiryRequestDto request, CancellationToken ct = default);
    Task<Result> DeleteAdmissionInquiryAsync(Guid id, CancellationToken ct = default);
    Task<Result<AdmissionInquiryDto>> ConvertAdmissionInquiryAsync(Guid id, CancellationToken ct = default);

    // Lookups
    Task<Result<IReadOnlyList<DropdownDto>>> GetActiveEmployeesAsync(CancellationToken ct = default);
}
