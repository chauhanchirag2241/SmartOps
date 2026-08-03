using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveTypeRepository
{
    Task<IList<LeaveTypeEntity>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<LeaveTypeEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<Guid> CreateAsync(LeaveTypeEntity entity, CancellationToken ct = default);
    Task UpdateAsync(LeaveTypeEntity entity, CancellationToken ct = default);
}
