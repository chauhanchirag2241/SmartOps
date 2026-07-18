using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class ClassFeeAmountRepository : BaseRepository, IClassFeeAmountRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IBranchContext _branchContext;

    private const string ClassDisplayNameSql =
        "c.classname || CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    private string EffectiveAmountSql =>
        $"""
        CASE
            WHEN ft.category = 4 THEN -ABS(
                CASE WHEN ft.frequency = 0
                    THEN COALESCE(NULLIF((
                        SELECT SUM(cfpa.amount)
                        FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
                        WHERE cfpa.classfeeamountid = cfa.id AND cfpa.isactive = true
                    ), 0), cfa.amount)
                    ELSE cfa.amount
                END
            )
            ELSE CASE WHEN ft.frequency = 0
                THEN COALESCE(NULLIF((
                    SELECT SUM(cfpa.amount)
                    FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
                    WHERE cfpa.classfeeamountid = cfa.id AND cfpa.isactive = true
                ), 0), cfa.amount)
                ELSE cfa.amount
            END
        END
        """;

    public ClassFeeAmountRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema,
        IBranchContext branchContext)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
        _branchContext = branchContext;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    public async Task<IList<ClassFeeSummaryRow>> GetClassSummariesAsync(
        Guid academicYearId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "c", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT c.id AS ClassId,
                   {ClassDisplayNameSql} AS ClassName,
                   (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                    INNER JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = sa.studentid AND s.isactive = true
                    WHERE sa.classid = c.id AND sa.academicyearid = @AcademicYearId AND sa.isactive = true) AS StudentCount,
                   COALESCE((
                       SELECT SUM({EffectiveAmountSql})
                       FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                       INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                       WHERE cfa.classid = c.id
                         AND cfa.feestructureversionid = @FeeStructureVersionId
                         AND cfa.isactive = true
                   ), 0) AS TotalAmount
            FROM {Schema}.{DatabaseConfig.TableClasses} c
            WHERE c.academicyearid = @AcademicYearId AND c.isactive = true{branchFilter}
            ORDER BY c.classname, c.section;
            """;
        IEnumerable<ClassFeeSummaryRow> rows = await connection
            .QueryAsync<ClassFeeSummaryRow>(new CommandDefinition(
                sql,
                new
                {
                    AcademicYearId = academicYearId,
                    FeeStructureVersionId = feeStructureVersionId,
                    ActiveBranchId = activeBranchId
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ClassFeeAmountRow>> GetAmountsByClassAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   COALESCE(cfa.amount, 0) AS Amount,
                   ft.ismandatory AS IsMandatory,
                   COALESCE(ft.studentwisedifferentamount, false) AS StudentWiseDifferentAmount
            FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                ON cfa.feetypeid = ft.id
               AND cfa.classid = @ClassId
               AND cfa.feestructureversionid = @FeeStructureVersionId
               AND cfa.isactive = true
            WHERE ft.feestructureversionid = @FeeStructureVersionId AND ft.isactive = true
            ORDER BY ft.name;
            """;
        List<ClassFeeAmountRow> rows = (await connection
            .QueryAsync<ClassFeeAmountRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();

        string periodsSql = $"""
            SELECT cfa.feetypeid AS FeeTypeId,
                   cfpa.periodindex AS PeriodIndex,
                   cfpa.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            INNER JOIN {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
              ON cfpa.classfeeamountid = cfa.id AND cfpa.isactive = true
            WHERE cfa.classid = @ClassId
              AND cfa.feestructureversionid = @FeeStructureVersionId
              AND cfa.isactive = true
            ORDER BY cfpa.periodindex;
            """;
        List<ClassFeePeriodAmountRow> periodRows = (await connection
            .QueryAsync<ClassFeePeriodAmountRow>(new CommandDefinition(
                periodsSql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();
        foreach (ClassFeeAmountRow row in rows)
        {
            row.PeriodAmounts = periodRows.Where(p => p.FeeTypeId == row.FeeTypeId).ToList();
        }
        return rows;
    }

    public async Task UpsertAmountsAsync(
        Guid classId,
        Guid academicYearId,
        Guid feeStructureVersionId,
        IList<ClassFeeAmountUpsertRow> amounts,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (ClassFeeAmountUpsertRow row in amounts)
            {
                string existsSql = $"""
                    SELECT id FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                    WHERE classid = @ClassId AND feetypeid = @FeeTypeId AND feestructureversionid = @FeeStructureVersionId;
                    """;
                Guid? existingId = await conn.ExecuteScalarAsync<Guid?>(
                    new CommandDefinition(
                        existsSql,
                        new { ClassId = classId, FeeTypeId = row.FeeTypeId, FeeStructureVersionId = feeStructureVersionId },
                        tx,
                        cancellationToken: ct))
                    .ConfigureAwait(false);

                Guid classFeeAmountId;
                if (existingId.HasValue)
                {
                    classFeeAmountId = existingId.Value;
                    await conn.ExecuteAsync(
                        $"""
                        UPDATE {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                        SET amount = @Amount,
                            isactive = true,
                            updatedby = @ActorId,
                            updatedon = @UtcNow,
                            versionno = versionno + 1
                        WHERE id = @Id;
                        """,
                        new { Id = classFeeAmountId, row.Amount, ActorId = actorId, UtcNow = utcNow },
                        tx).ConfigureAwait(false);
                }
                else
                {
                    if (row.Amount <= 0 && row.PeriodAmounts.All(p => p.Amount <= 0))
                    {
                        continue;
                    }

                    var entity = new ClassFeeAmountEntity
                    {
                        Id = Guid.NewGuid(),
                        FeeStructureVersionId = feeStructureVersionId,
                        ClassId = classId,
                        FeeTypeId = row.FeeTypeId,
                        AcademicYearId = academicYearId,
                        Amount = row.Amount,
                    };
                    EnsureInsertAudit(entity, utcNow, actorId);
                    classFeeAmountId = await InsertAsync(
                        conn,
                        Schema,
                        DatabaseConfig.TableClassFeeAmounts,
                        entity,
                        tx).ConfigureAwait(false);
                }

                await conn.ExecuteAsync(
                    $"""
                    UPDATE {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts}
                    SET isactive = false,
                        updatedby = @ActorId,
                        updatedon = @UtcNow,
                        versionno = versionno + 1
                    WHERE classfeeamountid = @ClassFeeAmountId AND isactive = true;
                    """,
                    new { ClassFeeAmountId = classFeeAmountId, ActorId = actorId, UtcNow = utcNow },
                    tx).ConfigureAwait(false);

                foreach (ClassFeePeriodAmountRow periodAmount in row.PeriodAmounts.OrderBy(p => p.PeriodIndex))
                {
                    var entity = new ClassFeePeriodAmountEntity
                    {
                        Id = Guid.NewGuid(),
                        ClassFeeAmountId = classFeeAmountId,
                        PeriodIndex = periodAmount.PeriodIndex,
                        Amount = periodAmount.Amount,
                    };
                    EnsureInsertAudit(entity, utcNow, actorId);
                    await InsertAsync(
                        conn,
                        Schema,
                        DatabaseConfig.TableClassFeePeriodAmounts,
                        entity,
                        tx).ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> ClassHasConfiguredAmountsAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT (
                EXISTS (
                    SELECT 1
                    FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                    INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                    WHERE cfa.classid = @ClassId
                      AND cfa.feestructureversionid = @FeeStructureVersionId
                      AND cfa.isactive = true
                      AND (
                          cfa.amount > 0
                          OR EXISTS (
                              SELECT 1
                              FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
                              WHERE cfpa.classfeeamountid = cfa.id
                                AND cfpa.isactive = true
                                AND cfpa.amount > 0)
                      )
                )
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
