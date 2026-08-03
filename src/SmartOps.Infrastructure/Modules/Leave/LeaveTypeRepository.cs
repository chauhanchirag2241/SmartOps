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

public sealed class LeaveTypeRepository : BaseRepository, ILeaveTypeRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public LeaveTypeRepository(
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

    public async Task<IList<LeaveTypeEntity>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        string sql = $"""
            SELECT id AS Id, code AS Code, name AS Name, ispaid AS IsPaid, requiresbalance AS RequiresBalance,
                   allowhalfday AS AllowHalfDay, carryforward AS CarryForward, sortorder AS SortOrder,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveTypes}
            WHERE 1 = 1{activeFilter}
            ORDER BY sortorder, name;
            """;
        var rows = await connection.QueryAsync<LeaveTypeEntity>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<LeaveTypeEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, code AS Code, name AS Name, ispaid AS IsPaid, requiresbalance AS RequiresBalance,
                   allowhalfday AS AllowHalfDay, carryforward AS CarryForward, sortorder AS SortOrder,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveTypes}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<LeaveTypeEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(1) FROM {Schema}.{DatabaseConfig.TableLeaveTypes}
            WHERE lower(code) = lower(@Code) AND (@ExcludeId IS NULL OR id <> @ExcludeId);
            """;
        int count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Code = code, ExcludeId = excludeId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return count > 0;
    }

    public async Task<Guid> CreateAsync(LeaveTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableLeaveTypes}
                (id, code, name, ispaid, requiresbalance, allowhalfday, carryforward, sortorder,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Code, @Name, @IsPaid, @RequiresBalance, @AllowHalfDay, @CarryForward, @SortOrder,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAsync(LeaveTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableLeaveTypes}
            SET name = @Name,
                ispaid = @IsPaid,
                requiresbalance = @RequiresBalance,
                allowhalfday = @AllowHalfDay,
                carryforward = @CarryForward,
                sortorder = @SortOrder,
                isactive = @IsActive,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }
}
