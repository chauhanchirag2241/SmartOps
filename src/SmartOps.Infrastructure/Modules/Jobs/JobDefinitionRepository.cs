using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Jobs.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Jobs.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Jobs;

public sealed class JobDefinitionRepository : BaseRepository, IJobDefinitionRepository
{
    public JobDefinitionRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IList<JobDefinitionEntity>> GetAllAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, code AS Code, name AS Name, description AS Description,
                   cronexpression AS CronExpression, timezoneid AS TimeZoneId,
                   isenabled AS IsEnabled, sortorder AS SortOrder,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {CatalogSchema}.{DatabaseConfig.TableJobDefinitions}
            WHERE isactive = true
            ORDER BY sortorder, name;
            """;
        var rows = await connection.QueryAsync<JobDefinitionEntity>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<JobDefinitionEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, code AS Code, name AS Name, description AS Description,
                   cronexpression AS CronExpression, timezoneid AS TimeZoneId,
                   isenabled AS IsEnabled, sortorder AS SortOrder,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {CatalogSchema}.{DatabaseConfig.TableJobDefinitions}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection.QuerySingleOrDefaultAsync<JobDefinitionEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateAsync(JobDefinitionEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {CatalogSchema}.{DatabaseConfig.TableJobDefinitions}
            SET cronexpression = @CronExpression,
                timezoneid = @TimeZoneId,
                isenabled = @IsEnabled,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<HangfireConfigEntity?> GetHangfireConfigAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, isenabled AS IsEnabled, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {CatalogSchema}.{DatabaseConfig.TableHangfireConfig}
            ORDER BY updatedon DESC
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<HangfireConfigEntity>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SetHangfireEnabledAsync(bool isEnabled, Guid updatedBy, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            UPDATE {CatalogSchema}.{DatabaseConfig.TableHangfireConfig}
            SET isenabled = @IsEnabled,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn;
            """;
        int affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { IsEnabled = isEnabled, UpdatedBy = updatedBy, UpdatedOn = SchoolLocalTime.NowDateTime() },
            cancellationToken: ct)).ConfigureAwait(false);

        if (affected == 0)
        {
            string insert = $"""
                INSERT INTO {CatalogSchema}.{DatabaseConfig.TableHangfireConfig}
                    (id, isenabled, updatedby, updatedon)
                VALUES
                    (@Id, @IsEnabled, @UpdatedBy, @UpdatedOn);
                """;
            await connection.ExecuteAsync(new CommandDefinition(
                insert,
                new
                {
                    Id = Guid.NewGuid(),
                    IsEnabled = isEnabled,
                    UpdatedBy = updatedBy,
                    UpdatedOn = SchoolLocalTime.NowDateTime()
                },
                cancellationToken: ct)).ConfigureAwait(false);
        }
    }
}
