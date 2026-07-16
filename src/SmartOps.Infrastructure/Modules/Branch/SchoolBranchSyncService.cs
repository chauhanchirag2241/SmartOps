using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Branch;

public sealed class SchoolBranchSyncService
{
    private readonly DapperContext _context;
    private readonly TenantContext _tenantContext;
    private readonly IDbConnectionFactory _connectionFactory;

    public SchoolBranchSyncService(
        DapperContext context,
        TenantContext tenantContext,
        IDbConnectionFactory connectionFactory)
    {
        _context = context;
        _tenantContext = tenantContext;
        _connectionFactory = connectionFactory;
    }

    public Task EnsureSyncedAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.UsesDedicatedDatabase)
        {
            return Task.CompletedTask;
        }

        return EnsureSyncedAsync(schoolId, _tenantContext.ConnectionString, cancellationToken);
    }

    public async Task EnsureSyncedAsync(
        Guid schoolId,
        string? connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        IDbConnection platform = await _context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var schoolDb = await _connectionFactory
            .CreateConnectionAsync(connectionString, cancellationToken)
            .ConfigureAwait(false);

        string g = DatabaseConfig.Schema_Global;

        IEnumerable<SchoolBranchEntity> branches = await platform.QueryAsync<SchoolBranchEntity>(
            $"""
SELECT id AS Id, schoolid AS SchoolId, name AS Name, email AS Email, address AS Address,
       isheadoffice AS IsHeadOffice, isactive AS IsActive, versionno AS VersionNo,
       createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {g}.{DatabaseConfig.TableSchoolBranches}
WHERE schoolid = @SchoolId AND isactive = true;
""",
            new { SchoolId = schoolId }).ConfigureAwait(false);

        foreach (SchoolBranchEntity branch in branches)
        {
            await schoolDb.ExecuteAsync(
                $"""
INSERT INTO {g}.{DatabaseConfig.TableSchoolBranches}
    (id, schoolid, name, email, address, isheadoffice, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (@Id, @SchoolId, @Name, @Email, @Address, @IsHeadOffice, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    email = EXCLUDED.email,
    address = EXCLUDED.address,
    isheadoffice = EXCLUDED.isheadoffice,
    isactive = EXCLUDED.isactive,
    updatedby = EXCLUDED.updatedby,
    updatedon = EXCLUDED.updatedon;
""",
                branch).ConfigureAwait(false);
        }
    }
}
