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
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "cg", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT cg.id AS ClassId,
                   cg.classname AS ClassName,
                   (SELECT COUNT(*)::int
                    FROM {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                    INNER JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = sa.studentid AND s.isactive = true
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid AND c.isactive = true
                    WHERE c.classgroupid = cg.id AND sa.academicyearid = @AcademicYearId AND sa.isactive = true) AS StudentCount,
                   COALESCE((
                       SELECT SUM({EffectiveAmountSql})
                       FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                       INNER JOIN {Schema}.{DatabaseConfig.TableFeeHead} ft ON ft.id = cfa.feeheadid AND ft.isactive = true
                       WHERE cfa.classgroupid = cg.id
                         AND cfa.feestructureid = @FeeStructureId
                         AND cfa.academicyearid = @AcademicYearId
                         AND cfa.isactive = true
                   ), 0) AS TotalAmount
            FROM {Schema}.{DatabaseConfig.TableClassGroups} cg
            WHERE cg.isactive = true{branchFilter}
            ORDER BY cg.classname;
            """;
        IEnumerable<ClassFeeSummaryRow> rows = await connection
            .QueryAsync<ClassFeeSummaryRow>(new CommandDefinition(
                sql,
                new
                {
                    AcademicYearId = academicYearId,
                    FeeStructureId = feeStructureId,
                    ActiveBranchId = activeBranchId
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ClassFeeAmountRow>> GetAmountsByClassAsync(
        Guid classId,
        Guid academicYearId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        Guid classGroupId = await ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS FeeHeadId,
                   ft.name AS FeeHeadName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   COALESCE(cfa.amount, 0) AS Amount,
                   ft.ismandatory AS IsMandatory,
                   COALESCE(ft.studentwisedifferentamount, false) AS StudentWiseDifferentAmount
            FROM {Schema}.{DatabaseConfig.TableFeeHead} ft
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                ON cfa.feeheadid = ft.id
               AND cfa.classgroupid = @ClassGroupId
               AND cfa.feestructureid = @FeeStructureId
               AND cfa.academicyearid = @AcademicYearId
               AND cfa.isactive = true
            WHERE ft.feestructureid = @FeeStructureId AND ft.isactive = true
            ORDER BY ft.name;
            """;
        List<ClassFeeAmountRow> rows = (await connection
            .QueryAsync<ClassFeeAmountRow>(new CommandDefinition(
                sql,
                new { ClassGroupId = classGroupId, FeeStructureId = feeStructureId, AcademicYearId = academicYearId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();

        string periodsSql = $"""
            SELECT cfa.feeheadid AS FeeHeadId,
                   cfpa.periodindex AS PeriodIndex,
                   cfpa.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            INNER JOIN {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
              ON cfpa.classfeeamountid = cfa.id AND cfpa.isactive = true
            WHERE cfa.classgroupid = @ClassGroupId
              AND cfa.feestructureid = @FeeStructureId
              AND cfa.academicyearid = @AcademicYearId
              AND cfa.isactive = true
            ORDER BY cfpa.periodindex;
            """;
        List<ClassFeePeriodAmountRow> periodRows = (await connection
            .QueryAsync<ClassFeePeriodAmountRow>(new CommandDefinition(
                periodsSql,
                new { ClassGroupId = classGroupId, FeeStructureId = feeStructureId, AcademicYearId = academicYearId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();
        foreach (ClassFeeAmountRow row in rows)
        {
            row.PeriodAmounts = periodRows.Where(p => p.FeeHeadId == row.FeeHeadId).ToList();
        }
        return rows;
    }

    public async Task UpsertAmountsAsync(
        Guid classId,
        Guid academicYearId,
        Guid feeStructureId,
        IList<ClassFeeAmountUpsertRow> amounts,
        CancellationToken ct = default)
    {
        Guid classGroupId = await ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            foreach (ClassFeeAmountUpsertRow row in amounts)
            {
                string existsSql = $"""
                    SELECT id FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                    WHERE classgroupid = @ClassGroupId
                      AND feeheadid = @FeeHeadId
                      AND feestructureid = @FeeStructureId
                      AND academicyearid = @AcademicYearId;
                    """;
                Guid? existingId = await conn.ExecuteScalarAsync<Guid?>(
                    new CommandDefinition(
                        existsSql,
                        new
                        {
                            ClassGroupId = classGroupId,
                            FeeHeadId = row.FeeHeadId,
                            FeeStructureId = feeStructureId,
                            AcademicYearId = academicYearId
                        },
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
                        FeeStructureId = feeStructureId,
                        ClassGroupId = classGroupId,
                        FeeHeadId = row.FeeHeadId,
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
        Guid academicYearId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        Guid classGroupId = await ResolveClassGroupIdAsync(classId, ct).ConfigureAwait(false);
        if (classGroupId == Guid.Empty)
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT (
                EXISTS (
                    SELECT 1
                    FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                    INNER JOIN {Schema}.{DatabaseConfig.TableFeeHead} ft ON ft.id = cfa.feeheadid AND ft.isactive = true
                    WHERE cfa.classgroupid = @ClassGroupId
                      AND cfa.feestructureid = @FeeStructureId
                      AND cfa.academicyearid = @AcademicYearId
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
                new { ClassGroupId = classGroupId, FeeStructureId = feeStructureId, AcademicYearId = academicYearId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Accepts either a class group id or a section (classes.id) and returns the class group id.
    /// Fee amounts are stored per class group; admission uses section ids.
    /// </summary>
    public async Task<Guid> ResolveClassGroupIdAsync(Guid classOrGroupId, CancellationToken ct = default)
    {
        if (classOrGroupId == Guid.Empty)
        {
            return Guid.Empty;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(
                (SELECT id FROM {Schema}.{DatabaseConfig.TableClassGroups} WHERE id = @Id LIMIT 1),
                (SELECT classgroupid FROM {Schema}.{DatabaseConfig.TableClasses} WHERE id = @Id LIMIT 1)
            );
            """;
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { Id = classOrGroupId }, cancellationToken: ct))
            .ConfigureAwait(false) ?? Guid.Empty;
    }
}
