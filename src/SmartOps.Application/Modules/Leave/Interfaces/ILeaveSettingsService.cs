namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveSettingsService
{
    Task<StaffLeaveApprovalSettings> GetStaffSettingsAsync(Guid schoolId, CancellationToken ct = default);

    Task<StudentLeaveApprovalSettings> GetStudentSettingsAsync(Guid schoolId, CancellationToken ct = default);
}

public sealed class StaffLeaveApprovalSettings
{
    public string ApprovalMode { get; set; } = string.Empty;

    public IReadOnlyList<string> ApproverUserTypeCodes { get; set; } = Array.Empty<string>();
}

public sealed class StudentLeaveApprovalSettings
{
    public string ApprovalMode { get; set; } = string.Empty;

    public string DefaultApprover { get; set; } = string.Empty;

    public int LongLeaveMinDays { get; set; }

    public IReadOnlyList<string> LongLeaveApproverUserTypeCodes { get; set; } = Array.Empty<string>();

    public bool LongLeaveTransferToPrincipal { get; set; }
}
