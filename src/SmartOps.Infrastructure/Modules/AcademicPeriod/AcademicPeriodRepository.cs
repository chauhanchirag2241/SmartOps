using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.AcademicPeriod;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.AcademicPeriod;

public sealed class AcademicPeriodRepository : BaseRepository, IAcademicPeriodRepository
{
    private readonly IBranchContext _branchContext;

    public AcademicPeriodRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IBranchContext branchContext)
        : base(context, currentUser)
    {
        _branchContext = branchContext;
    }

    public async Task<IReadOnlyList<AcademicPeriodClassSummary>> GetClassesAsync(
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "cg", cancellationToken)
            .ConfigureAwait(false);

        string sql = $"""
            SELECT cg.id AS ClassId,
                   cg.classname AS ClassName,
                   COUNT(p.id)::int AS PeriodCount
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassGroups} cg
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods} p
              ON p.classgroupid = cg.id
             AND p.isactive = true
            WHERE cg.isactive = true{branchFilter}
            GROUP BY cg.id, cg.classname
            ORDER BY cg.classname;
            """;

        IEnumerable<AcademicPeriodClassSummary> rows = await connection.QueryAsync<AcademicPeriodClassSummary>(
            new CommandDefinition(
                sql,
                new { ActiveBranchId = activeBranchId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ClassAcademicPeriodEntity>> GetByClassAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id,
                   classgroupid AS ClassGroupId,
                   periodindex AS PeriodIndex,
                   name AS Name
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE classgroupid = @ClassGroupId
              AND isactive = true
            ORDER BY periodindex;
            """;
        IEnumerable<ClassAcademicPeriodEntity> rows = await connection.QueryAsync<ClassAcademicPeriodEntity>(
            new CommandDefinition(
                sql,
                new { ClassGroupId = classId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ClassAcademicPeriodEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id,
                   classgroupid AS ClassGroupId,
                   periodindex AS PeriodIndex,
                   name AS Name,
                   isactive AS IsActive,
                   createdby AS CreatedBy,
                   createdon AS CreatedOn,
                   updatedby AS UpdatedBy,
                   updatedon AS UpdatedOn,
                   versionno AS VersionNo
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<ClassAcademicPeriodEntity>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(
        Guid classId,
        IReadOnlyList<ClassAcademicPeriodEntity> periods,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        string schema = Context.OperationalSchema;
        string table = DatabaseConfig.TableClassAcademicPeriods;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{table}
                SET isactive = false,
                    updatedby = @ActorId,
                    updatedon = @UtcNow,
                    versionno = versionno + 1
                WHERE classgroupid = @ClassGroupId
                  AND isactive = true;
                """,
                new { ClassGroupId = classId, ActorId = actorId, UtcNow = utcNow },
                tx).ConfigureAwait(false);

            foreach (ClassAcademicPeriodEntity period in periods.OrderBy(p => p.PeriodIndex))
            {
                period.Id = Guid.NewGuid();
                period.ClassGroupId = classId;
                period.Name = period.Name.Trim();
                EnsureInsertAudit(period, utcNow, actorId);
                await InsertAsync(conn, schema, table, period, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }
}
