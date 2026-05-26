using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class ClassFeeInstallmentRepository : BaseRepository, IClassFeeInstallmentRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public ClassFeeInstallmentRepository(
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

    public async Task<bool> IsInstallmentSchemaReadyAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @Schema
                  AND table_name = @InstallmentsTable
            )
            AND EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = @Schema
                  AND table_name = @ClassAmountsTable
                  AND column_name = 'semester1amount'
            )
            AND EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = @Schema
                  AND table_name = @AllocationsTable
                  AND column_name = 'installmentid'
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new
                {
                    Schema,
                    InstallmentsTable = DatabaseConfig.TableClassFeeInstallments,
                    ClassAmountsTable = DatabaseConfig.TableClassFeeAmounts,
                    AllocationsTable = DatabaseConfig.TableFeePaymentAllocations
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ClassFeeInstallmentRow>> GetByClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<ClassFeeInstallmentRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT cfi.id AS Id,
                   cfi.feetypeid AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   cfi.periodindex AS PeriodIndex,
                   cfi.periodlabel AS PeriodLabel,
                   cfi.periodstart AS PeriodStart,
                   cfi.periodend AS PeriodEnd,
                   cfi.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfi.feetypeid AND ft.isactive = true
            WHERE cfi.classid = @ClassId
              AND cfi.feestructureversionid = @FeeStructureVersionId
              AND cfi.isactive = true
            ORDER BY ft.name, cfi.periodindex;
            """;
        IEnumerable<ClassFeeInstallmentRow> rows = await connection
            .QueryAsync<ClassFeeInstallmentRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ClassFeeAmountForInstallmentRow>> GetClassAmountsForVersionAsync(
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
                   cfa.amount AS Amount,
                   COALESCE(cfa.semester1amount, 0) AS Semester1Amount,
                   COALESCE(cfa.semester2amount, 0) AS Semester2Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
            WHERE cfa.classid = @ClassId
              AND cfa.feestructureversionid = @FeeStructureVersionId
              AND cfa.isactive = true
              AND (
                  ft.category = {(int)FeeCategory.Discount}
                  OR cfa.amount > 0
                  OR cfa.semester1amount > 0
                  OR cfa.semester2amount > 0
              );
            """;
        IEnumerable<ClassFeeAmountForInstallmentRow> rows = await connection
            .QueryAsync<ClassFeeAmountForInstallmentRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<Guid>> GetClassIdsWithAmountsForVersionAsync(
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT classid
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
            WHERE feestructureversionid = @FeeStructureVersionId AND isactive = true AND amount > 0;
            """;
        IEnumerable<Guid> rows = await connection
            .QueryAsync<Guid>(new CommandDefinition(sql, new { FeeStructureVersionId = feeStructureVersionId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> VersionHasInstallmentPaymentsAsync(Guid feeStructureVersionId, CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS (
                SELECT 1
                FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
                WHERE fp.feestructureversionid = @FeeStructureVersionId
                  AND fpa.isactive = true
                  AND fpa.installmentid IS NOT NULL
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { FeeStructureVersionId = feeStructureVersionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task RegenerateForClassFeeTypeAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid feeTypeId,
        Guid academicYearId,
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods,
        CancellationToken ct = default)
    {
        if (periods.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableClassFeeInstallments}
                SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                WHERE classid = @ClassId
                  AND feestructureversionid = @FeeStructureVersionId
                  AND feetypeid = @FeeTypeId
                  AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deactivateSql,
                new
                {
                    ClassId = classId,
                    FeeStructureVersionId = feeStructureVersionId,
                    FeeTypeId = feeTypeId,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow
                },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            // Unique index ignores isactive — remove inactive rows so new periods can be inserted.
            string deleteInactiveSql = $"""
                DELETE FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                WHERE cfi.classid = @ClassId
                  AND cfi.feestructureversionid = @FeeStructureVersionId
                  AND cfi.feetypeid = @FeeTypeId
                  AND cfi.isactive = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                      WHERE fpa.installmentid = cfi.id
                        AND fpa.isactive = true
                  );
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deleteInactiveSql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId, FeeTypeId = feeTypeId },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            foreach (FeeInstallmentGenerator.InstallmentPeriod period in periods)
            {
                var entity = new ClassFeeInstallmentEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureVersionId = feeStructureVersionId,
                    ClassId = classId,
                    FeeTypeId = feeTypeId,
                    AcademicYearId = academicYearId,
                    PeriodIndex = period.PeriodIndex,
                    PeriodLabel = period.PeriodLabel,
                    PeriodStart = period.PeriodStart,
                    PeriodEnd = period.PeriodEnd,
                    Amount = period.Amount
                };
                EnsureInsertAudit(entity, utcNow, actorId);
                string insertSql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableClassFeeInstallments}
                        (id, feestructureversionid, classid, feetypeid, academicyearid,
                         periodindex, periodlabel, periodstart, periodend, amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureVersionId, @ClassId, @FeeTypeId, @AcademicYearId,
                         @PeriodIndex, @PeriodLabel, @PeriodStart, @PeriodEnd, @Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """;
                await conn.ExecuteAsync(new CommandDefinition(insertSql, entity, transaction: tx, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public Task RegenerateForClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default) =>
        RegenerateForClassVersionInternalAsync(classId, feeStructureVersionId, academicYearId, ct);

    public async Task RegenerateForVersionAsync(
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        IList<Guid> classIds = await GetClassIdsWithAmountsForVersionAsync(feeStructureVersionId, ct).ConfigureAwait(false);
        foreach (Guid classId in classIds)
        {
            await RegenerateForClassVersionInternalAsync(classId, feeStructureVersionId, academicYearId, ct).ConfigureAwait(false);
        }
    }

    public async Task<IList<InstallmentPaidRow>> GetPaidByInstallmentAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<InstallmentPaidRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT fpa.installmentid AS InstallmentId,
                   fpa.feetypeid AS FeeTypeId,
                   COALESCE(SUM(fpa.amount), 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
            INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
            WHERE fp.studentid = @StudentId
              AND fp.feestructureversionid = @FeeStructureVersionId
              AND fpa.isactive = true
              AND fpa.installmentid IS NOT NULL
            GROUP BY fpa.installmentid, fpa.feetypeid;
            """;
        IEnumerable<InstallmentPaidRow> rows = await connection
            .QueryAsync<InstallmentPaidRow>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> InstallmentBelongsToClassVersionAsync(
        Guid installmentId,
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments}
                WHERE id = @InstallmentId
                  AND classid = @ClassId
                  AND feestructureversionid = @FeeStructureVersionId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { InstallmentId = installmentId, ClassId = classId, FeeStructureVersionId = feeStructureVersionId },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> amounts = await GetClassAmountsForVersionAsync(
                classId,
                feeStructureVersionId,
                ct)
            .ConfigureAwait(false);
        if (amounts.Count == 0)
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByClassVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        var activeCountByFeeType = existing
            .GroupBy(e => e.FeeTypeId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (ClassFeeAmountForInstallmentRow row in amounts)
        {
            IList<FeeInstallmentGenerator.InstallmentPeriod> periods = await BuildPeriodsForRowAsync(
                academicYearId,
                row,
                ct).ConfigureAwait(false);
            if (periods.Count == 0)
            {
                continue;
            }

            int activeCount = activeCountByFeeType.GetValueOrDefault(row.FeeTypeId, 0);
            if (activeCount == periods.Count)
            {
                continue;
            }

            await RegenerateForClassFeeTypeAsync(
                classId,
                feeStructureVersionId,
                row.FeeTypeId,
                academicYearId,
                periods,
                ct).ConfigureAwait(false);
        }
    }

    private async Task RegenerateForClassVersionInternalAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> amounts =
            await GetClassAmountsForVersionAsync(classId, feeStructureVersionId, ct).ConfigureAwait(false);
        foreach (ClassFeeAmountForInstallmentRow row in amounts)
        {
            IList<FeeInstallmentGenerator.InstallmentPeriod> periods = await BuildPeriodsForRowAsync(
                academicYearId,
                row,
                ct).ConfigureAwait(false);
            await RegenerateForClassFeeTypeAsync(
                classId,
                feeStructureVersionId,
                row.FeeTypeId,
                academicYearId,
                periods,
                ct).ConfigureAwait(false);
        }
    }

    private async Task<IList<FeeInstallmentGenerator.InstallmentPeriod>> BuildPeriodsForRowAsync(
        Guid academicYearId,
        ClassFeeAmountForInstallmentRow row,
        CancellationToken ct)
    {
        (DateOnly start, DateOnly end) = await GetAcademicYearDatesAsync(academicYearId, ct).ConfigureAwait(false);
        IList<FeeInstallmentGenerator.SemesterWindow> semesters = await GetSemesterWindowsAsync(academicYearId, ct)
            .ConfigureAwait(false);
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            (FeeCollectionType)row.CollectionType,
            row.Amount,
            row.Semester1Amount,
            row.Semester2Amount,
            semesters,
            start,
            end);
        if (!FeeCategoryHelper.IsDiscount(row.Category))
        {
            return periods;
        }

        return periods
            .Select(p => new FeeInstallmentGenerator.InstallmentPeriod(
                p.PeriodIndex,
                p.PeriodLabel,
                p.PeriodStart,
                p.PeriodEnd,
                FeeCategoryHelper.SignedInstallmentAmount((FeeCategory)row.Category, p.Amount)))
            .ToList();
    }

    private async Task<IList<FeeInstallmentGenerator.SemesterWindow>> GetSemesterWindowsAsync(
        Guid academicYearId,
        CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT name AS Label, startdate AS Start, enddate AS End
            FROM {Schema}.{DatabaseConfig.TableAcademicYearSemesters}
            WHERE academicyearid = @AcademicYearId AND isactive = true
            ORDER BY semesterindex;
            """;
        IEnumerable<(string Label, DateOnly Start, DateOnly End)> rows = await connection
            .QueryAsync<(string Label, DateOnly Start, DateOnly End)>(
                new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.Select(r => new FeeInstallmentGenerator.SemesterWindow(r.Label, r.Start, r.End)).ToList();
    }

    private async Task<(DateOnly Start, DateOnly End)> GetAcademicYearDatesAsync(Guid academicYearId, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT startdate AS StartDate, enddate AS EndDate
            FROM {Schema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """;
        var row = await connection.QueryFirstOrDefaultAsync<(DateOnly StartDate, DateOnly EndDate)>(
            new CommandDefinition(sql, new { Id = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
        if (row.StartDate == default || row.EndDate == default)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            return (today, today.AddMonths(11));
        }

        return (row.StartDate, row.EndDate);
    }
}
