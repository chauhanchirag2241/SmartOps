using SmartOps.Application.Modules.FrontOffice;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FrontOffice;
using SmartOps.Domain.Modules.FrontOffice.Entities;

namespace SmartOps.Infrastructure.Modules.FrontOffice.Services;

public sealed class FrontOfficeService : IFrontOfficeService
{
    private readonly IFrontOfficeRepository _repo;

    public FrontOfficeService(IFrontOfficeRepository repo) => _repo = repo;

    // ── Complaint types ──────────────────────────────────────

    public async Task<Result<IList<ComplaintTypeDto>>> GetComplaintTypesAsync(
        string? activeFilter = "All",
        CancellationToken ct = default)
    {
        IList<ComplaintTypeEntity> rows = await _repo.GetComplaintTypesAsync(activeFilter, ct).ConfigureAwait(false);
        return Result<IList<ComplaintTypeDto>>.Success(rows.Select(MapComplaintType).ToList());
    }

    public async Task<Result<ComplaintTypeDto>> GetComplaintTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        ComplaintTypeEntity? entity = await _repo.GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
        return entity is null || !entity.IsActive
            ? Result<ComplaintTypeDto>.Failure("Complaint type not found.")
            : Result<ComplaintTypeDto>.Success(MapComplaintType(entity));
    }

    public async Task<Result<ComplaintTypeDto>> CreateComplaintTypeAsync(CreateComplaintTypeRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ComplaintTypeDto>.Failure("Name is required.");
        }

        var entity = new ComplaintTypeEntity
        {
            Name = request.Name.Trim(),
            Description = TrimOrNull(request.Description),
            DisplayOrder = request.DisplayOrder
        };
        Guid id = await _repo.CreateComplaintTypeAsync(entity, ct).ConfigureAwait(false);
        return await GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<ComplaintTypeDto>> UpdateComplaintTypeAsync(Guid id, UpdateComplaintTypeRequestDto request, CancellationToken ct = default)
    {
        ComplaintTypeEntity? entity = await _repo.GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result<ComplaintTypeDto>.Failure("Complaint type not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ComplaintTypeDto>.Failure("Name is required.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.DisplayOrder = request.DisplayOrder;
        await _repo.UpdateComplaintTypeAsync(entity, ct).ConfigureAwait(false);
        return await GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteComplaintTypeAsync(Guid id, CancellationToken ct = default)
    {
        ComplaintTypeEntity? entity = await _repo.GetComplaintTypeByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result.Failure("Complaint type not found.");
        }

        await _repo.SoftDeleteComplaintTypeAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    // ── Visitor purposes ─────────────────────────────────────

    public async Task<Result<IList<VisitorPurposeDto>>> GetVisitorPurposesAsync(
        string? activeFilter = "All",
        CancellationToken ct = default)
    {
        IList<VisitorPurposeEntity> rows = await _repo.GetVisitorPurposesAsync(activeFilter, ct).ConfigureAwait(false);
        return Result<IList<VisitorPurposeDto>>.Success(rows.Select(MapVisitorPurpose).ToList());
    }

    public async Task<Result<VisitorPurposeDto>> GetVisitorPurposeByIdAsync(Guid id, CancellationToken ct = default)
    {
        VisitorPurposeEntity? entity = await _repo.GetVisitorPurposeByIdAsync(id, ct).ConfigureAwait(false);
        return entity is null || !entity.IsActive
            ? Result<VisitorPurposeDto>.Failure("Visitor purpose not found.")
            : Result<VisitorPurposeDto>.Success(MapVisitorPurpose(entity));
    }

    public async Task<Result<VisitorPurposeDto>> CreateVisitorPurposeAsync(CreateVisitorPurposeRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<VisitorPurposeDto>.Failure("Name is required.");
        }

        var entity = new VisitorPurposeEntity
        {
            Name = request.Name.Trim(),
            Description = TrimOrNull(request.Description),
            DisplayOrder = request.DisplayOrder
        };
        Guid id = await _repo.CreateVisitorPurposeAsync(entity, ct).ConfigureAwait(false);
        return await GetVisitorPurposeByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<VisitorPurposeDto>> UpdateVisitorPurposeAsync(Guid id, UpdateVisitorPurposeRequestDto request, CancellationToken ct = default)
    {
        VisitorPurposeEntity? entity = await _repo.GetVisitorPurposeByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result<VisitorPurposeDto>.Failure("Visitor purpose not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<VisitorPurposeDto>.Failure("Name is required.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.DisplayOrder = request.DisplayOrder;
        await _repo.UpdateVisitorPurposeAsync(entity, ct).ConfigureAwait(false);
        return await GetVisitorPurposeByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteVisitorPurposeAsync(Guid id, CancellationToken ct = default)
    {
        VisitorPurposeEntity? entity = await _repo.GetVisitorPurposeByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result.Failure("Visitor purpose not found.");
        }

        await _repo.SoftDeleteVisitorPurposeAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    // ── Visitors ─────────────────────────────────────────────

    public async Task<Result<IList<VisitorDto>>> GetVisitorsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        IList<VisitorListRow> rows = await _repo.GetVisitorsAsync(activeFilter, fromDate, toDate, ct).ConfigureAwait(false);
        return Result<IList<VisitorDto>>.Success(rows.Select(MapVisitor).ToList());
    }

    public async Task<Result<VisitorDto>> GetVisitorByIdAsync(Guid id, CancellationToken ct = default)
    {
        VisitorListRow? row = await _repo.GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
        return row is null || !row.IsActive
            ? Result<VisitorDto>.Failure("Visitor not found.")
            : Result<VisitorDto>.Success(MapVisitor(row));
    }

    public async Task<Result<VisitorDto>> CreateVisitorAsync(CreateVisitorRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<VisitorDto>.Failure("Name is required.");
        }

        if (request.PurposeId == Guid.Empty)
        {
            return Result<VisitorDto>.Failure("Purpose is required.");
        }

        var entity = new VisitorEntity
        {
            Name = request.Name.Trim(),
            Phone = TrimOrNull(request.Phone),
            IdCardType = TrimOrNull(request.IdCardType),
            IdCardNumber = TrimOrNull(request.IdCardNumber),
            PurposeId = request.PurposeId,
            MeetingWith = TrimOrNull(request.MeetingWith),
            InTime = request.InTime,
            Note = TrimOrNull(request.Note),
            DocumentPath = TrimOrNull(request.DocumentPath)
        };
        Guid id = await _repo.CreateVisitorAsync(entity, ct).ConfigureAwait(false);
        return await GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<VisitorDto>> UpdateVisitorAsync(Guid id, UpdateVisitorRequestDto request, CancellationToken ct = default)
    {
        VisitorListRow? existing = await _repo.GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<VisitorDto>.Failure("Visitor not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<VisitorDto>.Failure("Name is required.");
        }

        if (request.PurposeId == Guid.Empty)
        {
            return Result<VisitorDto>.Failure("Purpose is required.");
        }

        var entity = new VisitorEntity
        {
            Id = id,
            Name = request.Name.Trim(),
            Phone = TrimOrNull(request.Phone),
            IdCardType = TrimOrNull(request.IdCardType),
            IdCardNumber = TrimOrNull(request.IdCardNumber),
            PurposeId = request.PurposeId,
            MeetingWith = TrimOrNull(request.MeetingWith),
            InTime = request.InTime,
            OutTime = request.OutTime,
            Note = TrimOrNull(request.Note),
            DocumentPath = TrimOrNull(request.DocumentPath)
        };
        await _repo.UpdateVisitorAsync(entity, ct).ConfigureAwait(false);
        return await GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteVisitorAsync(Guid id, CancellationToken ct = default)
    {
        VisitorListRow? existing = await _repo.GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result.Failure("Visitor not found.");
        }

        await _repo.SoftDeleteVisitorAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<VisitorDto>> CheckoutVisitorAsync(Guid id, CancellationToken ct = default)
    {
        VisitorListRow? existing = await _repo.GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<VisitorDto>.Failure("Visitor not found.");
        }

        if (existing.OutTime.HasValue)
        {
            return Result<VisitorDto>.Failure("Visitor already checked out.");
        }

        await _repo.CheckoutVisitorAsync(id, DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
        return await GetVisitorByIdAsync(id, ct).ConfigureAwait(false);
    }

    // ── Phone logs ───────────────────────────────────────────

    public async Task<Result<IList<PhoneLogDto>>> GetPhoneLogsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        IList<PhoneLogEntity> rows = await _repo.GetPhoneLogsAsync(activeFilter, fromDate, toDate, ct).ConfigureAwait(false);
        return Result<IList<PhoneLogDto>>.Success(rows.Select(MapPhoneLog).ToList());
    }

    public async Task<Result<PhoneLogDto>> GetPhoneLogByIdAsync(Guid id, CancellationToken ct = default)
    {
        PhoneLogEntity? entity = await _repo.GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
        return entity is null || !entity.IsActive
            ? Result<PhoneLogDto>.Failure("Phone log not found.")
            : Result<PhoneLogDto>.Success(MapPhoneLog(entity));
    }

    public async Task<Result<PhoneLogDto>> CreatePhoneLogAsync(CreatePhoneLogRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CallerName))
        {
            return Result<PhoneLogDto>.Failure("Caller name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Result<PhoneLogDto>.Failure("Description is required.");
        }

        var entity = new PhoneLogEntity
        {
            CallerName = request.CallerName.Trim(),
            Phone = TrimOrNull(request.Phone),
            CallType = request.CallType,
            CallDate = request.CallDate,
            Duration = TrimOrNull(request.Duration),
            Description = request.Description.Trim(),
            NextFollowUpDate = request.NextFollowUpDate,
            Note = TrimOrNull(request.Note)
        };
        Guid id = await _repo.CreatePhoneLogAsync(entity, ct).ConfigureAwait(false);
        return await GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<PhoneLogDto>> UpdatePhoneLogAsync(Guid id, UpdatePhoneLogRequestDto request, CancellationToken ct = default)
    {
        PhoneLogEntity? entity = await _repo.GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result<PhoneLogDto>.Failure("Phone log not found.");
        }

        if (string.IsNullOrWhiteSpace(request.CallerName))
        {
            return Result<PhoneLogDto>.Failure("Caller name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Result<PhoneLogDto>.Failure("Description is required.");
        }

        entity.CallerName = request.CallerName.Trim();
        entity.Phone = TrimOrNull(request.Phone);
        entity.CallType = request.CallType;
        entity.CallDate = request.CallDate;
        entity.Duration = TrimOrNull(request.Duration);
        entity.Description = request.Description.Trim();
        entity.NextFollowUpDate = request.NextFollowUpDate;
        entity.Note = TrimOrNull(request.Note);
        await _repo.UpdatePhoneLogAsync(entity, ct).ConfigureAwait(false);
        return await GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeletePhoneLogAsync(Guid id, CancellationToken ct = default)
    {
        PhoneLogEntity? entity = await _repo.GetPhoneLogByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null || !entity.IsActive)
        {
            return Result.Failure("Phone log not found.");
        }

        await _repo.SoftDeletePhoneLogAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    // ── Complaints ───────────────────────────────────────────

    public async Task<Result<IList<ComplaintDto>>> GetComplaintsAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default)
    {
        IList<ComplaintListRow> rows = await _repo.GetComplaintsAsync(activeFilter, fromDate, toDate, status, ct)
            .ConfigureAwait(false);
        return Result<IList<ComplaintDto>>.Success(rows.Select(MapComplaint).ToList());
    }

    public async Task<Result<ComplaintDto>> GetComplaintByIdAsync(Guid id, CancellationToken ct = default)
    {
        ComplaintListRow? row = await _repo.GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
        return row is null || !row.IsActive
            ? Result<ComplaintDto>.Failure("Complaint not found.")
            : Result<ComplaintDto>.Success(MapComplaint(row));
    }

    public async Task<Result<ComplaintDto>> CreateComplaintAsync(CreateComplaintRequestDto request, CancellationToken ct = default)
    {
        Result validation = ValidateComplaint(request.ComplaintTypeId, request.Description, request.AssignedToEmployeeId);
        if (!validation.IsSuccess)
        {
            return Result<ComplaintDto>.Failure(validation.Error!);
        }

        var entity = new ComplaintEntity
        {
            ComplaintTypeId = request.ComplaintTypeId,
            ComplaintDate = request.ComplaintDate,
            IsAnonymous = request.IsAnonymous,
            ComplainantName = request.IsAnonymous ? null : TrimOrNull(request.ComplainantName),
            Phone = TrimOrNull(request.Phone),
            Description = request.Description.Trim(),
            AssignedToEmployeeId = request.AssignedToEmployeeId,
            Status = request.Status,
            ActionTaken = TrimOrNull(request.ActionTaken),
            Note = TrimOrNull(request.Note),
            DocumentPath = TrimOrNull(request.DocumentPath)
        };
        Guid id = await _repo.CreateComplaintAsync(entity, ct).ConfigureAwait(false);
        return await GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<ComplaintDto>> UpdateComplaintAsync(Guid id, UpdateComplaintRequestDto request, CancellationToken ct = default)
    {
        ComplaintListRow? existing = await _repo.GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<ComplaintDto>.Failure("Complaint not found.");
        }

        Result validation = ValidateComplaint(request.ComplaintTypeId, request.Description, request.AssignedToEmployeeId);
        if (!validation.IsSuccess)
        {
            return Result<ComplaintDto>.Failure(validation.Error!);
        }

        var entity = new ComplaintEntity
        {
            Id = id,
            ComplaintTypeId = request.ComplaintTypeId,
            ComplaintDate = request.ComplaintDate,
            IsAnonymous = request.IsAnonymous,
            ComplainantName = request.IsAnonymous ? null : TrimOrNull(request.ComplainantName),
            Phone = TrimOrNull(request.Phone),
            Description = request.Description.Trim(),
            AssignedToEmployeeId = request.AssignedToEmployeeId,
            Status = request.Status,
            ActionTaken = TrimOrNull(request.ActionTaken),
            Note = TrimOrNull(request.Note),
            DocumentPath = TrimOrNull(request.DocumentPath)
        };
        await _repo.UpdateComplaintAsync(entity, ct).ConfigureAwait(false);
        return await GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteComplaintAsync(Guid id, CancellationToken ct = default)
    {
        ComplaintListRow? existing = await _repo.GetComplaintByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result.Failure("Complaint not found.");
        }

        await _repo.SoftDeleteComplaintAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    // ── Admission inquiries ──────────────────────────────────

    public async Task<Result<IList<AdmissionInquiryDto>>> GetAdmissionInquiriesAsync(
        string? activeFilter = "All",
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        int? status = null,
        CancellationToken ct = default)
    {
        IList<AdmissionInquiryListRow> rows = await _repo
            .GetAdmissionInquiriesAsync(activeFilter, fromDate, toDate, status, ct)
            .ConfigureAwait(false);
        return Result<IList<AdmissionInquiryDto>>.Success(rows.Select(MapInquiry).ToList());
    }

    public async Task<Result<AdmissionInquiryDto>> GetAdmissionInquiryByIdAsync(Guid id, CancellationToken ct = default)
    {
        AdmissionInquiryListRow? row = await _repo.GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
        return row is null || !row.IsActive
            ? Result<AdmissionInquiryDto>.Failure("Admission inquiry not found.")
            : Result<AdmissionInquiryDto>.Success(MapInquiry(row));
    }

    public async Task<Result<AdmissionInquiryDto>> CreateAdmissionInquiryAsync(CreateAdmissionInquiryRequestDto request, CancellationToken ct = default)
    {
        Result validation = ValidateInquiry(request.ParentName, request.StudentName);
        if (!validation.IsSuccess)
        {
            return Result<AdmissionInquiryDto>.Failure(validation.Error!);
        }

        var entity = MapInquiryRequest(request);
        Guid id = await _repo.CreateAdmissionInquiryAsync(entity, ct).ConfigureAwait(false);
        return await GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<AdmissionInquiryDto>> UpdateAdmissionInquiryAsync(Guid id, UpdateAdmissionInquiryRequestDto request, CancellationToken ct = default)
    {
        AdmissionInquiryListRow? existing = await _repo.GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<AdmissionInquiryDto>.Failure("Admission inquiry not found.");
        }

        Result validation = ValidateInquiry(request.ParentName, request.StudentName);
        if (!validation.IsSuccess)
        {
            return Result<AdmissionInquiryDto>.Failure(validation.Error!);
        }

        var entity = new AdmissionInquiryEntity
        {
            Id = id,
            ParentName = request.ParentName.Trim(),
            Phone = TrimOrNull(request.Phone),
            WhatsApp = TrimOrNull(request.WhatsApp),
            Email = TrimOrNull(request.Email),
            Address = TrimOrNull(request.Address),
            StudentName = request.StudentName.Trim(),
            ClassLabel = TrimOrNull(request.ClassLabel),
            InquiryDate = request.InquiryDate,
            NextFollowUpDate = request.NextFollowUpDate,
            AssignedToEmployeeId = request.AssignedToEmployeeId,
            Reference = TrimOrNull(request.Reference),
            Status = request.Status,
            Description = TrimOrNull(request.Description),
            AutoFollowUp = request.AutoFollowUp,
            StreamGroup = request.StreamGroup
        };
        await _repo.UpdateAdmissionInquiryAsync(entity, ct).ConfigureAwait(false);
        return await GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result> DeleteAdmissionInquiryAsync(Guid id, CancellationToken ct = default)
    {
        AdmissionInquiryListRow? existing = await _repo.GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result.Failure("Admission inquiry not found.");
        }

        await _repo.SoftDeleteAdmissionInquiryAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<AdmissionInquiryDto>> ConvertAdmissionInquiryAsync(Guid id, CancellationToken ct = default)
    {
        AdmissionInquiryListRow? existing = await _repo.GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null || !existing.IsActive)
        {
            return Result<AdmissionInquiryDto>.Failure("Admission inquiry not found.");
        }

        await _repo.ConvertAdmissionInquiryAsync(id, ct).ConfigureAwait(false);
        return await GetAdmissionInquiryByIdAsync(id, ct).ConfigureAwait(false);
    }

    // ── Lookups ──────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DropdownDto>>> GetActiveEmployeesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DropdownDto> items = await _repo.GetActiveEmployeesAsync(ct).ConfigureAwait(false);
        return Result<IReadOnlyList<DropdownDto>>.Success(items);
    }

    // ── Mapping / validation ─────────────────────────────────

    private static Result ValidateComplaint(Guid complaintTypeId, string description, Guid assignedToEmployeeId)
    {
        if (complaintTypeId == Guid.Empty)
        {
            return Result.Failure("Complaint type is required.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure("Description is required.");
        }

        if (assignedToEmployeeId == Guid.Empty)
        {
            return Result.Failure("Assigned employee is required.");
        }

        return Result.Success();
    }

    private static Result ValidateInquiry(string parentName, string studentName)
    {
        if (string.IsNullOrWhiteSpace(parentName))
        {
            return Result.Failure("Parent name is required.");
        }

        if (string.IsNullOrWhiteSpace(studentName))
        {
            return Result.Failure("Student name is required.");
        }

        return Result.Success();
    }

    private static AdmissionInquiryEntity MapInquiryRequest(CreateAdmissionInquiryRequestDto request) => new()
    {
        ParentName = request.ParentName.Trim(),
        Phone = TrimOrNull(request.Phone),
        WhatsApp = TrimOrNull(request.WhatsApp),
        Email = TrimOrNull(request.Email),
        Address = TrimOrNull(request.Address),
        StudentName = request.StudentName.Trim(),
        ClassLabel = TrimOrNull(request.ClassLabel),
        InquiryDate = request.InquiryDate,
        NextFollowUpDate = request.NextFollowUpDate,
        AssignedToEmployeeId = request.AssignedToEmployeeId,
        Reference = TrimOrNull(request.Reference),
        Status = request.Status,
        Description = TrimOrNull(request.Description),
        AutoFollowUp = request.AutoFollowUp,
        StreamGroup = request.StreamGroup
    };

    private static ComplaintTypeDto MapComplaintType(ComplaintTypeEntity e) =>
        new(e.Id, e.Name, e.Description, e.DisplayOrder, e.IsActive);

    private static VisitorPurposeDto MapVisitorPurpose(VisitorPurposeEntity e) =>
        new(e.Id, e.Name, e.Description, e.DisplayOrder, e.IsActive);

    private static VisitorDto MapVisitor(VisitorListRow r) =>
        new(r.Id, r.Name, r.Phone, r.IdCardType, r.IdCardNumber, r.PurposeId, r.PurposeName,
            r.MeetingWith, r.InTime, r.OutTime, r.Note, r.DocumentPath, r.IsActive);

    private static PhoneLogDto MapPhoneLog(PhoneLogEntity e) =>
        new(e.Id, e.CallerName, e.Phone, e.CallType, CallTypeLabel(e.CallType), e.CallDate,
            e.Duration, e.Description, e.NextFollowUpDate, e.Note, e.IsActive);

    private static ComplaintDto MapComplaint(ComplaintListRow r)
    {
        var status = (ComplaintStatus)r.Status;
        return new ComplaintDto(
            r.Id, r.ComplaintTypeId, r.ComplaintTypeName, r.ComplaintDate, r.IsAnonymous,
            r.ComplainantName, r.Phone, r.Description, r.AssignedToEmployeeId, r.AssignedToEmployeeName,
            status, ComplaintStatusLabel(status), r.ActionTaken, r.Note, r.DocumentPath, r.IsActive);
    }

    private static AdmissionInquiryDto MapInquiry(AdmissionInquiryListRow r)
    {
        var status = (InquiryStatus)r.Status;
        return new AdmissionInquiryDto(
            r.Id, r.ParentName, r.Phone, r.WhatsApp, r.Email, r.Address, r.StudentName, r.ClassLabel,
            r.InquiryDate, r.NextFollowUpDate, r.AssignedToEmployeeId, r.AssignedToEmployeeName,
            r.Reference, status, InquiryStatusLabel(status), r.Description, r.AutoFollowUp,
            r.StreamGroup, r.IsActive);
    }

    private static string CallTypeLabel(CallType t) => t switch
    {
        CallType.Incoming => "Incoming",
        CallType.Outgoing => "Outgoing",
        _ => t.ToString()
    };

    private static string ComplaintStatusLabel(ComplaintStatus s) => s switch
    {
        ComplaintStatus.Pending => "Pending",
        ComplaintStatus.InProgress => "In Progress",
        ComplaintStatus.Resolved => "Resolved",
        ComplaintStatus.Closed => "Closed",
        _ => s.ToString()
    };

    private static string InquiryStatusLabel(InquiryStatus s) => s switch
    {
        InquiryStatus.New => "New",
        InquiryStatus.FollowUp => "Follow-up",
        InquiryStatus.VisitScheduled => "Visit Scheduled",
        InquiryStatus.AdmissionForm => "Admission Form",
        InquiryStatus.Enrolled => "Enrolled",
        InquiryStatus.NotInterested => "Not Interested",
        _ => s.ToString()
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
