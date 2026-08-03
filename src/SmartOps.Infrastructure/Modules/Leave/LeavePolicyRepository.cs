using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Leave.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Leave;

public sealed class LeavePolicyRepository : BaseRepository, ILeavePolicyRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public LeavePolicyRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    public async Task<IList<LeavePolicyListRow>> GetAllAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT p.id AS Id, p.usertypeid AS UserTypeId,
                   p.leavetypeid AS LeaveTypeId, lt.name AS LeaveTypeName, lt.code AS LeaveTypeCode, p.monthlyleave AS MonthlyLeave
            FROM {Schema}.{DatabaseConfig.TableLeavePolicies} p
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = p.leavetypeid
            WHERE p.isactive = true
            ORDER BY lt.sortorder, lt.name;
            """;
        var rows = await connection.QueryAsync<LeavePolicyListRow>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<LeavePolicyListRow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT p.id AS Id, p.usertypeid AS UserTypeId,
                   p.leavetypeid AS LeaveTypeId, lt.name AS LeaveTypeName, lt.code AS LeaveTypeCode, p.monthlyleave AS MonthlyLeave
            FROM {Schema}.{DatabaseConfig.TableLeavePolicies} p
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = p.leavetypeid
            WHERE p.id = @Id AND p.isactive = true
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<LeavePolicyListRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateMonthlyLeaveAsync(Guid id, decimal monthlyLeave, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actor = ResolveUpdateActor();
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableLeavePolicies}
            SET monthlyleave = @MonthlyLeave,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, MonthlyLeave = monthlyLeave, UpdatedBy = actor, UpdatedOn = utcNow },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<LeavePolicyEntity?> GetByUserTypeAndLeaveTypeAsync(
        Guid userTypeId, Guid leaveTypeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, usertypeid AS UserTypeId, leavetypeid AS LeaveTypeId, monthlyleave AS MonthlyLeave,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableLeavePolicies}
            WHERE usertypeid = @UserTypeId AND leavetypeid = @LeaveTypeId
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<LeavePolicyEntity>(
            new CommandDefinition(sql, new { UserTypeId = userTypeId, LeaveTypeId = leaveTypeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> UpsertAsync(LeavePolicyEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();

        LeavePolicyEntity? existing = await GetByUserTypeAndLeaveTypeAsync(entity.UserTypeId, entity.LeaveTypeId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.MonthlyLeave = entity.MonthlyLeave;
            existing.IsActive = true;
            ApplyUpdateAudit(existing, ResolveUpdateActor(), utcNow);

            string updateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableLeavePolicies}
                SET monthlyleave = @MonthlyLeave,
                    isactive = true,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE id = @Id;
                """;
            await connection.ExecuteAsync(new CommandDefinition(updateSql, existing, cancellationToken: ct))
                .ConfigureAwait(false);
            return existing.Id;
        }

        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string insertSql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableLeavePolicies}
                (id, usertypeid, leavetypeid, monthlyleave,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @UserTypeId, @LeaveTypeId, @MonthlyLeave,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertSql, entity, cancellationToken: ct))
            .ConfigureAwait(false);
        return entity.Id;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await SoftDeleteAsync(connection, Schema, DatabaseConfig.TableLeavePolicies, id).ConfigureAwait(false);
    }
}
