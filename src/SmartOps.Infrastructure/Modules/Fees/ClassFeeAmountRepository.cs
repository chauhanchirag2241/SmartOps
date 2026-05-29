using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class ClassFeeAmountRepository : BaseRepository, IClassFeeAmountRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    private const string ClassDisplayNameSql =
        "c.classname || CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    private const string EffectiveAmountSql =
        """
        CASE
            WHEN ft.category = 4 THEN -ABS(
                CASE WHEN ft.frequency = 0
                    THEN COALESCE(NULLIF(cfa.semester1amount + cfa.semester2amount, 0), cfa.amount)
                    ELSE cfa.amount
                END
            )
            ELSE CASE WHEN ft.frequency = 0
                THEN COALESCE(NULLIF(cfa.semester1amount + cfa.semester2amount, 0), cfa.amount)
                ELSE cfa.amount
            END
        END
        """;

    public ClassFeeAmountRepository(
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

    public async Task<IList<ClassFeeSummaryRow>> GetClassSummariesAsync(
        Guid academicYearId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
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
            WHERE c.academicyearid = @AcademicYearId AND c.isactive = true
            ORDER BY c.classname, c.section;
            """;
        IEnumerable<ClassFeeSummaryRow> rows = await connection
            .QueryAsync<ClassFeeSummaryRow>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, FeeStructureVersionId = feeStructureVersionId },
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
                   COALESCE(cfa.semester1amount, 0) AS Semester1Amount,
                   COALESCE(cfa.semester2amount, 0) AS Semester2Amount,
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
        IEnumerable<ClassFeeAmountRow> rows = await connection
            .QueryAsync<ClassFeeAmountRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
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

        foreach (ClassFeeAmountUpsertRow row in amounts)
        {
            string existsSql = $"""
                SELECT id FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                WHERE classid = @ClassId AND feetypeid = @FeeTypeId AND feestructureversionid = @FeeStructureVersionId;
                """;
            Guid? existingId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    existsSql,
                    new { ClassId = classId, FeeTypeId = row.FeeTypeId, FeeStructureVersionId = feeStructureVersionId },
                    cancellationToken: ct))
                .ConfigureAwait(false);

            if (existingId.HasValue)
            {
                string updateSql = $"""
                    UPDATE {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                    SET amount = @Amount,
                        semester1amount = @Semester1Amount,
                        semester2amount = @Semester2Amount,
                        isactive = true,
                        updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                    WHERE id = @Id;
                    """;
                await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new
                    {
                        Id = existingId.Value,
                        row.Amount,
                        row.Semester1Amount,
                        row.Semester2Amount,
                        UpdatedBy = actorId,
                        UpdatedOn = utcNow
                    },
                    cancellationToken: ct)).ConfigureAwait(false);
            }
            else if (row.Amount > 0 || row.Semester1Amount > 0 || row.Semester2Amount > 0)
            {
                var entity = new ClassFeeAmountEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureVersionId = feeStructureVersionId,
                    ClassId = classId,
                    FeeTypeId = row.FeeTypeId,
                    AcademicYearId = academicYearId,
                    Amount = row.Amount,
                    Semester1Amount = row.Semester1Amount,
                    Semester2Amount = row.Semester2Amount
                };
                EnsureInsertAudit(entity, utcNow, actorId);
                string insertSql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                        (id, feestructureversionid, classid, feetypeid, academicyearid, amount,
                         semester1amount, semester2amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureVersionId, @ClassId, @FeeTypeId, @AcademicYearId, @Amount,
                         @Semester1Amount, @Semester2Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """;
                await connection.ExecuteAsync(new CommandDefinition(insertSql, entity, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
        }
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
                          OR cfa.semester1amount > 0
                          OR cfa.semester2amount > 0
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
