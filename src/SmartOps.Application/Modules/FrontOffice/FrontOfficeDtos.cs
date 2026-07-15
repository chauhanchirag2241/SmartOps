using SmartOps.Domain.Modules.FrontOffice;

namespace SmartOps.Application.Modules.FrontOffice;

// ── Complaint types ──────────────────────────────────────────

public record ComplaintTypeDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public record CreateComplaintTypeRequestDto(
    string Name,
    string? Description,
    int DisplayOrder = 0);

public record UpdateComplaintTypeRequestDto(
    string Name,
    string? Description,
    int DisplayOrder = 0);

// ── Visitor purposes ─────────────────────────────────────────

public record VisitorPurposeDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public record CreateVisitorPurposeRequestDto(
    string Name,
    string? Description,
    int DisplayOrder = 0);

public record UpdateVisitorPurposeRequestDto(
    string Name,
    string? Description,
    int DisplayOrder = 0);

// ── Visitors ─────────────────────────────────────────────────

public record VisitorDto(
    Guid Id,
    string Name,
    string? Phone,
    string? IdCardType,
    string? IdCardNumber,
    Guid PurposeId,
    string? PurposeName,
    string? MeetingWith,
    DateTimeOffset InTime,
    DateTimeOffset? OutTime,
    string? Note,
    string? DocumentPath,
    bool IsActive);

public record CreateVisitorRequestDto(
    string Name,
    string? Phone,
    string? IdCardType,
    string? IdCardNumber,
    Guid PurposeId,
    string? MeetingWith,
    DateTimeOffset InTime,
    DateTimeOffset? OutTime,
    string? Note,
    string? DocumentPath);

public record UpdateVisitorRequestDto(
    string Name,
    string? Phone,
    string? IdCardType,
    string? IdCardNumber,
    Guid PurposeId,
    string? MeetingWith,
    DateTimeOffset InTime,
    DateTimeOffset? OutTime,
    string? Note,
    string? DocumentPath);

// ── Phone logs ───────────────────────────────────────────────

public record PhoneLogDto(
    Guid Id,
    string CallerName,
    string? Phone,
    CallType CallType,
    string CallTypeLabel,
    DateOnly CallDate,
    string? Duration,
    string Description,
    DateOnly? NextFollowUpDate,
    string? Note,
    bool IsActive);

public record CreatePhoneLogRequestDto(
    string CallerName,
    string? Phone,
    CallType CallType,
    DateOnly CallDate,
    string? Duration,
    string Description,
    DateOnly? NextFollowUpDate,
    string? Note);

public record UpdatePhoneLogRequestDto(
    string CallerName,
    string? Phone,
    CallType CallType,
    DateOnly CallDate,
    string? Duration,
    string Description,
    DateOnly? NextFollowUpDate,
    string? Note);

// ── Complaints ───────────────────────────────────────────────

public record ComplaintDto(
    Guid Id,
    Guid ComplaintTypeId,
    string? ComplaintTypeName,
    DateOnly ComplaintDate,
    bool IsAnonymous,
    string? ComplainantName,
    string? Phone,
    string Description,
    Guid AssignedToEmployeeId,
    string? AssignedToEmployeeName,
    ComplaintStatus Status,
    string StatusLabel,
    string? ActionTaken,
    string? Note,
    string? DocumentPath,
    bool IsActive);

public record CreateComplaintRequestDto(
    Guid ComplaintTypeId,
    DateOnly ComplaintDate,
    bool IsAnonymous,
    string? ComplainantName,
    string? Phone,
    string Description,
    Guid AssignedToEmployeeId,
    ComplaintStatus Status,
    string? ActionTaken,
    string? Note,
    string? DocumentPath);

public record UpdateComplaintRequestDto(
    Guid ComplaintTypeId,
    DateOnly ComplaintDate,
    bool IsAnonymous,
    string? ComplainantName,
    string? Phone,
    string Description,
    Guid AssignedToEmployeeId,
    ComplaintStatus Status,
    string? ActionTaken,
    string? Note,
    string? DocumentPath);

// ── Admission inquiries ──────────────────────────────────────

public record AdmissionInquiryDto(
    Guid Id,
    string ParentName,
    string? Phone,
    string? WhatsApp,
    string? Email,
    string? Address,
    string StudentName,
    string? ClassLabel,
    DateOnly InquiryDate,
    DateOnly? NextFollowUpDate,
    Guid? AssignedToEmployeeId,
    string? AssignedToEmployeeName,
    string? Reference,
    InquiryStatus Status,
    string StatusLabel,
    string? Description,
    bool AutoFollowUp,
    short? StreamGroup,
    bool IsActive);

public record CreateAdmissionInquiryRequestDto(
    string ParentName,
    string? Phone,
    string? WhatsApp,
    string? Email,
    string? Address,
    string StudentName,
    string? ClassLabel,
    DateOnly InquiryDate,
    DateOnly? NextFollowUpDate,
    Guid? AssignedToEmployeeId,
    string? Reference,
    InquiryStatus Status,
    string? Description,
    bool AutoFollowUp,
    short? StreamGroup = null);

public record UpdateAdmissionInquiryRequestDto(
    string ParentName,
    string? Phone,
    string? WhatsApp,
    string? Email,
    string? Address,
    string StudentName,
    string? ClassLabel,
    DateOnly InquiryDate,
    DateOnly? NextFollowUpDate,
    Guid? AssignedToEmployeeId,
    string? Reference,
    InquiryStatus Status,
    string? Description,
    bool AutoFollowUp,
    short? StreamGroup = null);
