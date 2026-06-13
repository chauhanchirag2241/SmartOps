using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveRepository
{
    Task<Guid> CreateAsync(LeaveRequestEntity entity, CancellationToken ct = default);
    Task UpdateAsync(LeaveRequestEntity entity, CancellationToken ct = default);
    Task<LeaveRequestEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<LeaveListRow>> GetStaffListAsync(string? statusFilter, Guid? employeeid, DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IList<LeaveListRow>> GetStudentListAsync(string? statusFilter, Guid? studentId, CancellationToken ct = default);
    Task<IList<LeaveListRow>> GetMineAsync(LeaveRequestType requestType, Guid userId, CancellationToken ct = default);
    Task<bool> HasOverlappingApprovedAsync(LeaveRequestType type, Guid? employeeid, Guid? studentId, DateOnly from, DateOnly to, Guid? excludeId, CancellationToken ct = default);
    Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Guid?> GetClassIdForStudentAsync(Guid studentId, CancellationToken ct = default);
    Task<Guid?> GetClassTeacherUserIdAsync(Guid classId, CancellationToken ct = default);
    Task<IList<Guid>> GetSchoolAdminUserIdsAsync(Guid schoolId, CancellationToken ct = default);
    Task<IList<SchoolAdminUserRow>> GetSchoolAdminUsersAsync(Guid schoolId, CancellationToken ct = default);
    Task<bool> IsParentLinkedToStudentAsync(Guid parentUserId, Guid studentId, CancellationToken ct = default);
    Task<IList<Guid>> GetActiveTeacherUserIdsAsync(CancellationToken ct = default);
    Task<IList<Guid>> GetParentUserIdsForClassAsync(Guid classId, CancellationToken ct = default);
    Task<LeaveDetailRow?> GetDetailRowAsync(Guid id, CancellationToken ct = default);
    Task<IList<LinkedStudentRow>> GetLinkedStudentsForParentAsync(Guid parentUserId, CancellationToken ct = default);
}

public sealed class SchoolAdminUserRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public sealed class LinkedStudentRow
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? ClassName { get; set; }
}

public class LeaveListRow
{
    public Guid Id { get; set; }
    public short RequestType { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? TeacherFirstName { get; set; }
    public string? TeacherLastName { get; set; }
    public Guid? StudentId { get; set; }
    public string? StudentFirstName { get; set; }
    public string? StudentLastName { get; set; }
    public string? ClassName { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByEmail { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public short? LeaveType { get; set; }
    public short Status { get; set; }
    public DateTime CreatedOn { get; set; }
}

public sealed class LeaveDetailRow : LeaveListRow
{
    public string? Reason { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByEmail { get; set; }
    public DateTimeOffset? ApprovedOn { get; set; }
    public string? ApproverRemark { get; set; }
}
