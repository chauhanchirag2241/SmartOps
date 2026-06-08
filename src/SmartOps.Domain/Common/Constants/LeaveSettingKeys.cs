namespace SmartOps.Domain.Common.Constants;

public static class LeaveSettingKeys
{
    public const string Prefix = "leave.";

    public const string StaffApprovalMode = "leave.staff.approvalMode";
    public const string StaffApproverUserTypes = "leave.staff.approverUserTypes";

    public const string StudentApprovalMode = "leave.student.approvalMode";
    public const string StudentDefaultApprover = "leave.student.defaultApprover";
    public const string StudentLongLeaveMinDays = "leave.student.longLeaveMinDays";
    public const string StudentLongLeaveApproverUserTypes = "leave.student.longLeaveApproverUserTypes";
    public const string StudentLongLeaveTransferToPrincipal = "leave.student.longLeaveTransferToPrincipal";

    public static readonly string[] AllLeaveKeys =
    [
        StaffApprovalMode,
        StaffApproverUserTypes,
        StudentApprovalMode,
        StudentDefaultApprover,
        StudentLongLeaveMinDays,
        StudentLongLeaveApproverUserTypes,
        StudentLongLeaveTransferToPrincipal,
    ];
}
