using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Branch;

/// <summary>
/// Retired stub. Branch source-of-truth is school DB <c>man.schoolbranches</c>;
/// platform no longer syncs branches into tenant databases.
/// Kept registered in DI so existing constructors do not break.
/// </summary>
public sealed class SchoolBranchSyncService
{
    private readonly ILogger<SchoolBranchSyncService> _logger;
    private bool _loggedNoOp;

    public SchoolBranchSyncService(
        DapperContext context,
        TenantContext tenantContext,
        IDbConnectionFactory connectionFactory,
        ILogger<SchoolBranchSyncService>? logger = null)
    {
        _ = context;
        _ = tenantContext;
        _ = connectionFactory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SchoolBranchSyncService>.Instance;
    }

    public Task EnsureSyncedAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        _ = schoolId;
        _ = cancellationToken;
        LogRetiredOnce();
        return Task.CompletedTask;
    }

    public Task EnsureSyncedAsync(
        Guid schoolId,
        string? connectionString,
        CancellationToken cancellationToken = default)
    {
        _ = schoolId;
        _ = connectionString;
        _ = cancellationToken;
        LogRetiredOnce();
        return Task.CompletedTask;
    }

    private void LogRetiredOnce()
    {
        if (_loggedNoOp)
        {
            return;
        }

        _loggedNoOp = true;
        _logger.LogDebug(
            "SchoolBranchSyncService.EnsureSyncedAsync is a no-op; branch sync to school DB is retired.");
    }
}
