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
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "c", cancellationToken)
            .ConfigureAwait(false);

        string sql = $"""
            SELECT c.id AS ClassId,
                   c.classname ||
                     CASE c.section
                       WHEN 1 THEN ' - A' WHEN 2 THEN ' - B'
                       WHEN 3 THEN ' - C' WHEN 4 THEN ' - D'
                       ELSE ''
                     END AS ClassName,
                   c.academicyearid AS AcademicYearId,
                   COUNT(p.id)::int AS PeriodCount,
                   MIN(p.periodtype) AS PeriodType
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClasses} c
            LEFT JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods} p
              ON p.classid = c.id AND p.isactive = true
            WHERE c.academicyearid = @AcademicYearId
              AND c.isactive = true{branchFilter}
            GROUP BY c.id, c.classname, c.section, c.academicyearid
            ORDER BY c.classname, c.section;
            """;

        IEnumerable<AcademicPeriodClassSummary> rows = await connection.QueryAsync<AcademicPeriodClassSummary>(
            new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, ActiveBranchId = activeBranchId },
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
                   classid AS ClassId,
                   academicyearid AS AcademicYearId,
                   periodtype AS PeriodType,
                   periodindex AS PeriodIndex,
                   name AS Name,
                   startdate AS StartDate,
                   enddate AS EndDate
            FROM {Context.OperationalSchema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE classid = @ClassId AND isactive = true
            ORDER BY periodindex;
            """;
        IEnumerable<ClassAcademicPeriodEntity> rows = await connection.QueryAsync<ClassAcademicPeriodEntity>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SaveAsync(
        Guid classId,
        Guid academicYearId,
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
                WHERE classid = @ClassId AND isactive = true;
                """,
                new { ClassId = classId, ActorId = actorId, UtcNow = utcNow },
                tx).ConfigureAwait(false);

            foreach (ClassAcademicPeriodEntity period in periods.OrderBy(p => p.PeriodIndex))
            {
                period.Id = Guid.NewGuid();
                period.ClassId = classId;
                period.AcademicYearId = academicYearId;
                period.Name = period.Name.Trim();
                EnsureInsertAudit(period, utcNow, actorId);
                await InsertAsync(conn, schema, table, period, tx).ConfigureAwait(false);
            }

            await conn.ExecuteAsync(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                SET periodlabel = p.name,
                    periodstart = p.startdate,
                    periodend = p.enddate,
                    updatedby = @ActorId,
                    updatedon = @UtcNow,
                    versionno = cfi.versionno + 1
                FROM {schema}.{DatabaseConfig.TableClassAcademicPeriods} p
                WHERE cfi.classid = @ClassId
                  AND cfi.periodindex = p.periodindex
                  AND cfi.isactive = true
                  AND EXISTS (
                      SELECT 1
                      FROM {schema}.{DatabaseConfig.TableFeeTypes} ft
                      WHERE ft.id = cfi.feetypeid
                        AND ft.frequency = 0
                        AND ft.isactive = true)
                  AND p.classid = @ClassId
                  AND p.isactive = true;

                UPDATE {schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                SET isactive = false,
                    updatedby = @ActorId,
                    updatedon = @UtcNow,
                    versionno = cfi.versionno + 1
                WHERE cfi.classid = @ClassId
                  AND cfi.isactive = true
                  AND EXISTS (
                      SELECT 1
                      FROM {schema}.{DatabaseConfig.TableFeeTypes} ft
                      WHERE ft.id = cfi.feetypeid
                        AND ft.frequency = 0
                        AND ft.isactive = true)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM {schema}.{DatabaseConfig.TableClassAcademicPeriods} p
                      WHERE p.classid = @ClassId
                        AND p.periodindex = cfi.periodindex
                        AND p.isactive = true);

                UPDATE {schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                SET periodlabel = cfi.periodlabel,
                    periodstart = cfi.periodstart,
                    periodend = cfi.periodend,
                    isactive = cfi.isactive,
                    updatedby = @ActorId,
                    updatedon = @UtcNow,
                    versionno = sfi.versionno + 1
                FROM {schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                WHERE sfi.classfeeinstallmentid = cfi.id
                  AND cfi.classid = @ClassId
                  AND sfi.isactive = true;
                """,
                new { ClassId = classId, ActorId = actorId, UtcNow = utcNow },
                tx).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<bool> HasPaidInstallmentsAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {Context.OperationalSchema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableFeePaymentAllocations} a
                    ON a.installmentid = sfi.id AND a.isactive = true
                INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                    ON cfi.id = sfi.classfeeinstallmentid
                INNER JOIN {Context.OperationalSchema}.{DatabaseConfig.TableFeeTypes} ft
                    ON ft.id = cfi.feetypeid AND ft.frequency = 0
                WHERE cfi.classid = @ClassId);
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { ClassId = classId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
