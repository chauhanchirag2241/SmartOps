using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Jobs.Interfaces;

public interface IJobMasterService
{
    Task<Result<JobMasterPageDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<JobDefinitionDto>> UpdateAsync(Guid id, UpdateJobDefinitionDto request, CancellationToken ct = default);
    Task<Result<HangfireStatusDto>> GetHangfireStatusAsync(CancellationToken ct = default);
    Task<Result<HangfireStatusDto>> SetHangfireEnabledAsync(bool isEnabled, CancellationToken ct = default);
}
