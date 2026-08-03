using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Jobs;
using SmartOps.Application.Modules.Jobs.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Jobs.Entities;

namespace SmartOps.Infrastructure.Modules.Jobs.Services;

public sealed class JobMasterService : IJobMasterService
{
    private readonly IJobDefinitionRepository _repo;
    private readonly IHangfireRuntime _hangfireRuntime;
    private readonly IHangfireJobSync _jobSync;
    private readonly ICurrentUserService _currentUser;

    public JobMasterService(
        IJobDefinitionRepository repo,
        IHangfireRuntime hangfireRuntime,
        IHangfireJobSync jobSync,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _hangfireRuntime = hangfireRuntime;
        _jobSync = jobSync;
        _currentUser = currentUser;
    }

    public async Task<Result<JobMasterPageDto>> GetAllAsync(CancellationToken ct = default)
    {
        IList<JobDefinitionEntity> rows = await _repo.GetAllAsync(ct).ConfigureAwait(false);
        HangfireConfigEntity? config = await _repo.GetHangfireConfigAsync(ct).ConfigureAwait(false);
        return Result<JobMasterPageDto>.Success(new JobMasterPageDto(
            config?.IsEnabled ?? false,
            rows.Select(Map).ToList()));
    }

    public async Task<Result<JobDefinitionDto>> UpdateAsync(
        Guid id, UpdateJobDefinitionDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            return Result<JobDefinitionDto>.Failure("Cron expression is required.");
        }

        JobDefinitionEntity? entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<JobDefinitionDto>.Failure("Job definition not found.");
        }

        entity.CronExpression = request.CronExpression.Trim();
        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            entity.TimeZoneId = request.TimeZoneId.Trim();
        }

        entity.IsEnabled = request.IsEnabled;
        await _repo.UpdateAsync(entity, ct).ConfigureAwait(false);

        HangfireConfigEntity? config = await _repo.GetHangfireConfigAsync(ct).ConfigureAwait(false);
        if (config?.IsEnabled == true)
        {
            _jobSync.SyncJob(entity.Code, entity.CronExpression, entity.TimeZoneId, entity.IsEnabled);
        }

        return Result<JobDefinitionDto>.Success(Map(entity));
    }

    public async Task<Result<HangfireStatusDto>> GetHangfireStatusAsync(CancellationToken ct = default)
    {
        HangfireConfigEntity? config = await _repo.GetHangfireConfigAsync(ct).ConfigureAwait(false);
        return Result<HangfireStatusDto>.Success(new HangfireStatusDto(config?.IsEnabled ?? false));
    }

    public async Task<Result<HangfireStatusDto>> SetHangfireEnabledAsync(bool isEnabled, CancellationToken ct = default)
    {
        Guid actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);

        await _repo.SetHangfireEnabledAsync(isEnabled, actor, ct).ConfigureAwait(false);

        if (isEnabled)
        {
            await _hangfireRuntime.EnableAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await _hangfireRuntime.DisableAsync(ct).ConfigureAwait(false);
        }

        return Result<HangfireStatusDto>.Success(new HangfireStatusDto(isEnabled));
    }

    private static JobDefinitionDto Map(JobDefinitionEntity e) =>
        new(e.Id, e.Code, e.Name, e.Description, e.CronExpression, e.TimeZoneId, e.IsEnabled, e.SortOrder);
}
