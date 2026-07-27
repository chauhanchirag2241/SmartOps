using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.StaffAttendance.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.StaffAttendance.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.StaffAttendance;

public sealed class EmployeeFaceEnrollmentRepository : BaseRepository, IEmployeeFaceEnrollmentRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public EmployeeFaceEnrollmentRepository(
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

    public async Task<EmployeeFaceEnrollmentEntity?> GetActiveByEmployeeAsync(
        Guid employeeId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, employeeid, embedding, photourl, modelname,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments}
            WHERE employeeid = @EmployeeId AND isactive = true
            LIMIT 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<EmployeeFaceEnrollmentEntity>(
            new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<EmployeeFaceEnrollmentEntity>> ListActiveForTenantAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, employeeid, embedding, photourl, modelname,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments}
            WHERE isactive = true;
            """;

        IEnumerable<EmployeeFaceEnrollmentEntity> rows = await connection.QueryAsync<EmployeeFaceEnrollmentEntity>(
            new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows.ToList();
    }

    public async Task UpsertAsync(EmployeeFaceEnrollmentEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            // Deactivate any existing active enrollment for this employee.
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments}
                SET isactive = false,
                    updatedby = @ActorId,
                    updatedon = @UtcNow,
                    versionno = versionno + 1
                WHERE employeeid = @EmployeeId AND isactive = true;
                """;

            await conn.ExecuteAsync(new CommandDefinition(
                deactivateSql,
                new { EmployeeId = entity.EmployeeId, ActorId = actorId, UtcNow = utcNow },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            EnsureInsertAudit(entity, utcNow, actorId);

            string insertSql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments}
                    (id, employeeid, embedding, photourl, modelname,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @EmployeeId, @Embedding, @PhotoUrl, @ModelName,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                entity.Id,
                entity.EmployeeId,
                entity.Embedding,
                entity.PhotoUrl,
                entity.ModelName,
                entity.IsActive,
                entity.VersionNo,
                entity.CreatedBy,
                entity.CreatedOn,
                entity.UpdatedBy,
                entity.UpdatedOn
            }, transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task DeactivateAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveUpdateActor();

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployeeFaceEnrollments}
            SET isactive = false,
                updatedby = @ActorId,
                updatedon = @UtcNow,
                versionno = versionno + 1
            WHERE employeeid = @EmployeeId AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            EmployeeId = employeeId,
            ActorId = actorId,
            UtcNow = utcNow
        }, cancellationToken: ct)).ConfigureAwait(false);
    }
}
