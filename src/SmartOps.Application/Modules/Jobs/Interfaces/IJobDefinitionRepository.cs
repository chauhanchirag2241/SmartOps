using SmartOps.Domain.Modules.Jobs.Entities;

namespace SmartOps.Application.Modules.Jobs.Interfaces;

public interface IJobDefinitionRepository
{
    Task<IList<JobDefinitionEntity>> GetAllAsync(CancellationToken ct = default);
    Task<JobDefinitionEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(JobDefinitionEntity entity, CancellationToken ct = default);
    Task<HangfireConfigEntity?> GetHangfireConfigAsync(CancellationToken ct = default);
    Task SetHangfireEnabledAsync(bool isEnabled, Guid updatedBy, CancellationToken ct = default);
}
