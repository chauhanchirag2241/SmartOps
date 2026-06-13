using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveService
{
    Task<Result<IList<LeaveListItemDto>>> GetStaffListAsync(string? status, Guid? employeeid, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<Result<IList<LeaveListItemDto>>> GetStaffMineAsync(CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IList<LeaveApproverDto>>> GetStaffApproversAsync(CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> CreateStaffAsync(CreateLeaveRequestDto request, CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> SubmitStaffAsync(Guid id, CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> CancelAsync(Guid id, CancellationToken ct = default);
    Task<Result<IList<LeaveListItemDto>>> GetStudentListAsync(string? status, Guid? studentId, CancellationToken ct = default);
    Task<Result<IList<LeaveListItemDto>>> GetStudentMineAsync(CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> CreateStudentAsync(CreateStudentLeaveRequestDto request, CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> SubmitStudentAsync(Guid id, CancellationToken ct = default);
    Task<Result<IList<LinkedStudentDto>>> GetLinkedStudentsForParentAsync(CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> ApproveAsync(Guid leaveId, string? remark, CancellationToken ct = default);
    Task<Result<LeaveDetailDto>> RejectAsync(Guid leaveId, string? remark, CancellationToken ct = default);
}
