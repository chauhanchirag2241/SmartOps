using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeavePolicyRepository
{
    Task<IList<LeavePolicyListRow>> GetAllAsync(CancellationToken ct = default);
    Task<LeavePolicyListRow?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LeavePolicyEntity?> GetByUserTypeAndLeaveTypeAsync(Guid userTypeId, Guid leaveTypeId, CancellationToken ct = default);
    Task UpdateMonthlyLeaveAsync(Guid id, decimal monthlyLeave, CancellationToken ct = default);
    Task<Guid> UpsertAsync(LeavePolicyEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class LeavePolicyListRow
{
    public Guid Id { get; set; }
    public Guid UserTypeId { get; set; }
    public string? UserTypeName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public string? LeaveTypeCode { get; set; }
    public decimal MonthlyLeave { get; set; }
}
