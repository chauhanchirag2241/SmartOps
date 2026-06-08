using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveApproverResolver : ILeaveApproverResolver
{
    private readonly ILeaveSettingsService _settings;
    private readonly ILeaveRepository _leaveRepo;
    private readonly IUserTypeRepository _userTypes;

    public LeaveApproverResolver(
        ILeaveSettingsService settings,
        ILeaveRepository leaveRepo,
        IUserTypeRepository userTypes)
    {
        _settings = settings;
        _leaveRepo = leaveRepo;
        _userTypes = userTypes;
    }

    public async Task<LeaveApproverResolution> ResolveAsync(
        LeaveRequestEntity leave,
        Guid schoolId,
        CancellationToken ct = default)
    {
        if (leave.RequestType == LeaveRequestType.Staff)
        {
            StaffLeaveApprovalSettings staff = await _settings.GetStaffSettingsAsync(schoolId, ct).ConfigureAwait(false);
            IList<Guid> assignees = await ResolveUserTypeCodesAsync(schoolId, staff.ApproverUserTypeCodes, ct)
                .ConfigureAwait(false);
            assignees = await EnsureAssigneesAsync(schoolId, assignees, ct).ConfigureAwait(false);

            return new LeaveApproverResolution
            {
                AssigneeUserIds = assignees.ToList(),
                ApprovalMode = staff.ApprovalMode,
            };
        }

        StudentLeaveApprovalSettings student = await _settings.GetStudentSettingsAsync(schoolId, ct).ConfigureAwait(false);
        int dayCount = leave.ToDate.DayNumber - leave.FromDate.DayNumber + 1;
        bool useLongLeave = student.LongLeaveTransferToPrincipal && dayCount > student.LongLeaveMinDays;

        IList<Guid> resolved;
        if (useLongLeave)
        {
            resolved = await ResolveUserTypeCodesAsync(schoolId, student.LongLeaveApproverUserTypeCodes, ct)
                .ConfigureAwait(false);
        }
        else if (string.Equals(student.DefaultApprover, LeaveApproverTokens.ClassTeacher, StringComparison.OrdinalIgnoreCase)
                 && leave.StudentId.HasValue)
        {
            resolved = await ResolveClassTeacherAsync(leave.StudentId.Value, ct).ConfigureAwait(false);
        }
        else
        {
            resolved = await ResolveUserTypeCodesAsync(schoolId, [student.DefaultApprover], ct).ConfigureAwait(false);
        }

        resolved = await EnsureAssigneesAsync(schoolId, resolved, ct).ConfigureAwait(false);

        return new LeaveApproverResolution
        {
            AssigneeUserIds = resolved.ToList(),
            ApprovalMode = student.ApprovalMode,
        };
    }

    private async Task<IList<Guid>> ResolveClassTeacherAsync(Guid studentId, CancellationToken ct)
    {
        Guid? classId = await _leaveRepo.GetClassIdForStudentAsync(studentId, ct).ConfigureAwait(false);
        if (!classId.HasValue)
        {
            return [];
        }

        Guid? classTeacherUserId = await _leaveRepo.GetClassTeacherUserIdAsync(classId.Value, ct).ConfigureAwait(false);
        return classTeacherUserId.HasValue ? [classTeacherUserId.Value] : [];
    }

    private async Task<IList<Guid>> ResolveUserTypeCodesAsync(
        Guid schoolId,
        IReadOnlyList<string> codes,
        CancellationToken ct)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        IReadOnlyList<Guid> ids = await _userTypes.GetUserIdsByTypeCodesAsync(schoolId, codes, ct).ConfigureAwait(false);
        return ids.ToList();
    }

    private async Task<IList<Guid>> EnsureAssigneesAsync(Guid schoolId, IList<Guid> assignees, CancellationToken ct)
    {
        if (assignees.Count > 0)
        {
            return assignees;
        }

        IList<Guid> fallback = await _leaveRepo.GetSchoolAdminUserIdsAsync(schoolId, ct).ConfigureAwait(false);
        return fallback;
    }
}
