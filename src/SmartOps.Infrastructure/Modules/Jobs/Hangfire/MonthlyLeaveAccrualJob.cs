using Microsoft.Extensions.Logging;
using SmartOps.Application.Modules.Leave.Interfaces;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Modules.Jobs.Hangfire;

public sealed class MonthlyLeaveAccrualJob
{
    private readonly ILeaveAccrualService _accrualService;
    private readonly ILogger<MonthlyLeaveAccrualJob> _logger;

    public MonthlyLeaveAccrualJob(ILeaveAccrualService accrualService, ILogger<MonthlyLeaveAccrualJob> logger)
    {
        _accrualService = accrualService;
        _logger = logger;
    }

    public async Task Execute(CancellationToken ct = default)
    {
        DateTime now = SchoolLocalTime.NowDateTime();
        // Accrue for the previous calendar month (job typically runs on the 1st).
        DateTime target = now.AddMonths(-1);
        int year = target.Year;
        int month = target.Month;

        _logger.LogInformation("Starting monthly leave accrual for {Year}-{Month:D2}", year, month);
        Domain.Common.Result result = await _accrualService.RunAllSchoolsAsync(year, month, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Monthly leave accrual completed with errors: {Error}", result.Error);
            throw new InvalidOperationException(result.Error ?? "Leave accrual failed.");
        }

        _logger.LogInformation("Monthly leave accrual completed successfully for {Year}-{Month:D2}", year, month);
    }
}
