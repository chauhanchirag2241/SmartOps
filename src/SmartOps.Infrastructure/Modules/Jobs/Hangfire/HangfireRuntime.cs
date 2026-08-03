using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Modules.Jobs.Interfaces;
using SmartOps.Domain.Modules.Jobs.Entities;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

/// <summary>
/// Controllable Hangfire server: when disabled the BackgroundJobServer is disposed (zero DB polling).
/// </summary>
public sealed class HangfireRuntime : IHangfireRuntime, IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHangfireJobSync _jobSync;
    private readonly ILogger<HangfireRuntime> _logger;
    private readonly object _gate = new();
    private BackgroundJobServer? _server;
    private bool _disposed;

    public HangfireRuntime(
        IServiceScopeFactory scopeFactory,
        IHangfireJobSync jobSync,
        ILogger<HangfireRuntime> logger)
    {
        _scopeFactory = scopeFactory;
        _jobSync = jobSync;
        _logger = logger;
    }

    public bool IsServerRunning
    {
        get
        {
            lock (_gate)
            {
                return _server is not null;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        ApplyFromDatabaseAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisableAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ApplyFromDatabaseAsync(CancellationToken ct = default) =>
        EnableOrDisableFromDbAsync(ct);

    public async Task EnableAsync(CancellationToken ct = default)
    {
        await SyncAndStartAsync(ct).ConfigureAwait(false);
    }

    public async Task DisableAsync(CancellationToken ct = default)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IJobDefinitionRepository repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        IList<JobDefinitionEntity> jobs = await repo.GetAllAsync(ct).ConfigureAwait(false);
        _jobSync.RemoveAllJobs(jobs.Select(j => j.Code));

        lock (_gate)
        {
            if (_server is not null)
            {
                _server.Dispose();
                _server = null;
                _logger.LogInformation("Hangfire BackgroundJobServer stopped (no polling).");
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task EnableOrDisableFromDbAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IJobDefinitionRepository repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        HangfireConfigEntity? config = await repo.GetHangfireConfigAsync(ct).ConfigureAwait(false);

        if (config?.IsEnabled == true)
        {
            await SyncAndStartAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await DisableAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task SyncAndStartAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IJobDefinitionRepository repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        IList<JobDefinitionEntity> jobs = await repo.GetAllAsync(ct).ConfigureAwait(false);

        _jobSync.SyncAllJobs(jobs.Select(j => (j.Code, j.CronExpression, j.TimeZoneId, j.IsEnabled)));

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_server is null)
            {
                _server = new BackgroundJobServer();
                _logger.LogInformation("Hangfire BackgroundJobServer started.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _server?.Dispose();
            _server = null;
        }
    }
}
