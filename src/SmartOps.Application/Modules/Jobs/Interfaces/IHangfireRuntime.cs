namespace SmartOps.Application.Modules.Jobs.Interfaces;

public interface IHangfireRuntime
{
    bool IsServerRunning { get; }
    Task EnableAsync(CancellationToken ct = default);
    Task DisableAsync(CancellationToken ct = default);
    Task ApplyFromDatabaseAsync(CancellationToken ct = default);
}

public interface IHangfireJobSync
{
    void SyncJob(string code, string cronExpression, string timeZoneId, bool isEnabled);
    void SyncAllJobs(IEnumerable<(string Code, string CronExpression, string TimeZoneId, bool IsEnabled)> jobs);
    void RemoveAllJobs(IEnumerable<string> codes);
}
