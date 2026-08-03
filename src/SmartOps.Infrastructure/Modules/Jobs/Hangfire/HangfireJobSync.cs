using Hangfire;
using Microsoft.Extensions.Logging;
using SmartOps.Application.Modules.Jobs.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

public sealed class HangfireJobSync : IHangfireJobSync
{
    private readonly ILogger<HangfireJobSync> _logger;

    public HangfireJobSync(ILogger<HangfireJobSync> logger) => _logger = logger;

    public void SyncJob(string code, string cronExpression, string timeZoneId, bool isEnabled)
    {
        if (!isEnabled)
        {
            RecurringJob.RemoveIfExists(code);
            _logger.LogInformation("Removed Hangfire recurring job {Code}", code);
            return;
        }

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
            _logger.LogWarning("Unknown time zone {TimeZoneId}; using UTC for job {Code}", timeZoneId, code);
        }
        catch (InvalidTimeZoneException)
        {
            tz = TimeZoneInfo.Utc;
            _logger.LogWarning("Invalid time zone {TimeZoneId}; using UTC for job {Code}", timeZoneId, code);
        }

        switch (code)
        {
            case JobCodes.MonthlyLeaveAccrual:
                RecurringJob.AddOrUpdate<MonthlyLeaveAccrualJob>(
                    code,
                    job => job.Execute(CancellationToken.None),
                    cronExpression,
                    new RecurringJobOptions { TimeZone = tz });
                break;
            default:
                _logger.LogWarning("No Hangfire handler registered for job code {Code}", code);
                RecurringJob.RemoveIfExists(code);
                break;
        }
    }

    public void SyncAllJobs(IEnumerable<(string Code, string CronExpression, string TimeZoneId, bool IsEnabled)> jobs)
    {
        foreach ((string code, string cron, string tz, bool enabled) in jobs)
        {
            SyncJob(code, cron, tz, enabled);
        }
    }

    public void RemoveAllJobs(IEnumerable<string> codes)
    {
        foreach (string code in codes)
        {
            RecurringJob.RemoveIfExists(code);
        }
    }
}
