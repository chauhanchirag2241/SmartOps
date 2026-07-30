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
                FROM information_schema.tables
                WHERE table_schema = @Schema
                  AND table_name = @PeriodAmountsTable
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
                    PeriodAmountsTable = DatabaseConfig.TableClassFeePeriodAmounts,
                    AllocationsTable = DatabaseConfig.TableFeePaymentAllocations
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ClassFeeInstallmentRow>> GetByClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<ClassFeeInstallmentRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT cfi.id AS Id,
                   cfi.feeheadid AS FeeHeadId,
                   ft.name AS FeeHeadName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   cfi.periodindex AS PeriodIndex,
                   cfi.periodlabel AS PeriodLabel,
                   cfi.periodstart AS PeriodStart,
                   cfi.periodend AS PeriodEnd,
                   cfi.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeHead} ft ON ft.id = cfi.feeheadid AND ft.isactive = true
            WHERE cfi.classgroupid = @ClassGroupId
              AND cfi.feestructureid = @FeeStructureId
              AND cfi.isactive = true
            ORDER BY ft.name, cfi.periodindex;
            """;
        IEnumerable<ClassFeeInstallmentRow> rows = await connection
            .QueryAsync<ClassFeeInstallmentRow>(new CommandDefinition(
                sql,
                new { ClassGroupId = classId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ClassFeeAmountForInstallmentRow>> GetClassAmountsForVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS FeeHeadId,
                   ft.name AS FeeHeadName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   cfa.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeHead} ft ON ft.id = cfa.feeheadid AND ft.isactive = true
            WHERE cfa.classgroupid = @ClassGroupId
              AND cfa.feestructureid = @FeeStructureId
              AND cfa.academicyearid = @AcademicYearId
              AND cfa.isactive = true
              AND (
                  ft.category = {(int)FeeCategory.Discount}
                  OR cfa.amount > 0
                  OR EXISTS (
                      SELECT 1
                      FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
                      WHERE cfpa.classfeeamountid = cfa.id
                        AND cfpa.isactive = true
                        AND cfpa.amount > 0)
              );
            """;
        List<ClassFeeAmountForInstallmentRow> rows = (await connection
            .QueryAsync<ClassFeeAmountForInstallmentRow>(new CommandDefinition(
                sql,
                new { ClassGroupId = classId, FeeStructureId = feeStructureId, AcademicYearId = academicYearId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();

        string periodSql = $"""
            SELECT cfa.feeheadid AS FeeHeadId,
                   cfpa.periodindex AS PeriodIndex,
                   cfpa.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            INNER JOIN {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
              ON cfpa.classfeeamountid = cfa.id AND cfpa.isactive = true
            WHERE cfa.classgroupid = @ClassGroupId
              AND cfa.feestructureid = @FeeStructureId
              AND cfa.academicyearid = @AcademicYearId
              AND cfa.isactive = true;
            """;
        List<ClassFeePeriodAmountRow> periodAmounts = (await connection
            .QueryAsync<ClassFeePeriodAmountRow>(new CommandDefinition(
                periodSql,
                new { ClassGroupId = classId, FeeStructureId = feeStructureId, AcademicYearId = academicYearId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();
        foreach (ClassFeeAmountForInstallmentRow row in rows)
        {
            row.PeriodAmounts = periodAmounts.Where(p => p.FeeHeadId == row.FeeHeadId).ToList();
        }
        return rows;
    }

    public async Task<IList<Guid>> GetClassIdsWithAmountsForVersionAsync(
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT DISTINCT cfa.classgroupid
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
            WHERE cfa.feestructureid = @FeeStructureId
              AND cfa.isactive = true
              AND (
                  cfa.amount > 0
                  OR EXISTS (
                      SELECT 1
                      FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts} cfpa
                      WHERE cfpa.classfeeamountid = cfa.id
                        AND cfpa.isactive = true
                        AND cfpa.amount > 0));
            """;
        IEnumerable<Guid> rows = await connection
            .QueryAsync<Guid>(new CommandDefinition(sql, new { FeeStructureId = feeStructureId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> VersionHasInstallmentPaymentsAsync(Guid feeStructureId, CancellationToken ct = default)
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
                WHERE fp.feestructureid = @FeeStructureId
                  AND fpa.isactive = true
                  AND fpa.installmentid IS NOT NULL
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { FeeStructureId = feeStructureId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task RegenerateForClassFeeHeadAsync(
        Guid classId,
        Guid feeStructureId,
        Guid feeHeadId,
        Guid academicYearId,
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableClassFeeInstallments}
                SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                WHERE classgroupid = @ClassGroupId
                  AND feestructureid = @FeeStructureId
                  AND feeheadid = @FeeHeadId
                  AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deactivateSql,
                new
                {
                    ClassGroupId = classId,
                    FeeStructureId = feeStructureId,
                    FeeHeadId = feeHeadId,
                    UpdatedBy = actorId,
                    UpdatedOn = utcNow
                },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            // Unique index ignores isactive — remove inactive rows so new periods can be inserted.
            string deleteInactiveSql = $"""
                DELETE FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                WHERE cfi.classgroupid = @ClassGroupId
                  AND cfi.feestructureid = @FeeStructureId
                  AND cfi.feeheadid = @FeeHeadId
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
                new { ClassGroupId = classId, FeeStructureId = feeStructureId, FeeHeadId = feeHeadId },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            foreach (FeeInstallmentGenerator.InstallmentPeriod period in periods)
            {
                var entity = new ClassFeeInstallmentEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureId = feeStructureId,
                    ClassGroupId = classId,
                    FeeHeadId = feeHeadId,
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
                        (id, feestructureid, classgroupid, feeheadid, academicyearid,
                         periodindex, periodlabel, periodstart, periodend, amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureId, @ClassGroupId, @FeeHeadId, @AcademicYearId,
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
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default) =>
        RegenerateForClassVersionInternalAsync(classId, feeStructureId, academicYearId, ct);

    public async Task RegenerateForVersionAsync(
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        IList<Guid> classIds = await GetClassIdsWithAmountsForVersionAsync(feeStructureId, ct).ConfigureAwait(false);
        foreach (Guid classId in classIds)
        {
            await RegenerateForClassVersionInternalAsync(classId, feeStructureId, academicYearId, ct).ConfigureAwait(false);
        }
    }

    public async Task<IList<InstallmentPaidRow>> GetPaidByInstallmentAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<InstallmentPaidRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT fpa.installmentid AS InstallmentId,
                   fpa.feeheadid AS FeeHeadId,
                   COALESCE(SUM(fpa.amount), 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
            INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
            WHERE fp.studentid = @StudentId
              AND fp.feestructureid = @FeeStructureId
              AND fpa.isactive = true
              AND fpa.installmentid IS NOT NULL
            GROUP BY fpa.installmentid, fpa.feeheadid;
            """;
        IEnumerable<InstallmentPaidRow> rows = await connection
            .QueryAsync<InstallmentPaidRow>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> InstallmentBelongsToClassVersionAsync(
        Guid installmentId,
        Guid classId,
        Guid feeStructureId,
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
                  AND classgroupid = @ClassGroupId
                  AND feestructureid = @FeeStructureId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { InstallmentId = installmentId, ClassGroupId = classId, FeeStructureId = feeStructureId },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task EnsureMissingInstallmentsForClassVersionAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> amounts = await GetClassAmountsForVersionAsync(
                classId,
                feeStructureId,
                academicYearId,
                ct)
            .ConfigureAwait(false);
        if (amounts.Count == 0)
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByClassVersionAsync(classId, feeStructureId, ct)
            .ConfigureAwait(false);
        var existingByFeeHead = existing
            .GroupBy(e => e.FeeHeadId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.PeriodIndex).ToList());

        foreach (ClassFeeAmountForInstallmentRow row in amounts)
        {
            IList<FeeInstallmentGenerator.InstallmentPeriod> periods = await BuildPeriodsForRowAsync(
                classId,
                academicYearId,
                row,
                ct).ConfigureAwait(false);
            if (periods.Count == 0)
            {
                continue;
            }

            List<ClassFeeInstallmentRow> current =
                existingByFeeHead.GetValueOrDefault(row.FeeHeadId) ?? [];
            bool matches = current.Count == periods.Count
                && current.Zip(periods.OrderBy(p => p.PeriodIndex)).All(pair =>
                    pair.First.PeriodIndex == pair.Second.PeriodIndex
                    && pair.First.PeriodLabel == pair.Second.PeriodLabel
                    && pair.First.PeriodStart == pair.Second.PeriodStart
                    && pair.First.PeriodEnd == pair.Second.PeriodEnd
                    && pair.First.Amount == pair.Second.Amount);
            if (matches)
            {
                continue;
            }

            await RegenerateForClassFeeHeadAsync(
                classId,
                feeStructureId,
                row.FeeHeadId,
                academicYearId,
                periods,
                ct).ConfigureAwait(false);
        }
    }

    private async Task RegenerateForClassVersionInternalAsync(
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct)
    {
        if (!await IsInstallmentSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> amounts =
            await GetClassAmountsForVersionAsync(classId, feeStructureId, academicYearId, ct).ConfigureAwait(false);
        foreach (ClassFeeAmountForInstallmentRow row in amounts)
        {
            IList<FeeInstallmentGenerator.InstallmentPeriod> periods = await BuildPeriodsForRowAsync(
                classId,
                academicYearId,
                row,
                ct).ConfigureAwait(false);
            await RegenerateForClassFeeHeadAsync(
                classId,
                feeStructureId,
                row.FeeHeadId,
                academicYearId,
                periods,
                ct).ConfigureAwait(false);
        }
    }

    private async Task<IList<FeeInstallmentGenerator.InstallmentPeriod>> BuildPeriodsForRowAsync(
        Guid classId,
        Guid academicYearId,
        ClassFeeAmountForInstallmentRow row,
        CancellationToken ct)
    {
        (DateOnly start, DateOnly end) = await GetAcademicYearDatesAsync(academicYearId, ct).ConfigureAwait(false);
        IList<FeeInstallmentGenerator.PeriodWindow> periodWindows = await GetPeriodWindowsAsync(classId, start, end, ct)
            .ConfigureAwait(false);
        IList<FeeInstallmentGenerator.InstallmentPeriod> periods = FeeInstallmentGenerator.Generate(
            (FeeCollectionType)row.CollectionType,
            row.Amount,
            row.PeriodAmounts
                .Select(p => new FeeInstallmentGenerator.PeriodAmount(p.PeriodIndex, p.Amount))
                .ToList(),
            periodWindows,
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

    private async Task<IList<FeeInstallmentGenerator.PeriodWindow>> GetPeriodWindowsAsync(
        Guid classId,
        DateOnly yearStart,
        DateOnly yearEnd,
        CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT p.periodindex AS PeriodIndex,
                   p.name AS Label
            FROM {Schema}.{DatabaseConfig.TableClassAcademicPeriods} p
            WHERE p.classgroupid = @ClassGroupId
              AND p.isactive = true
            ORDER BY p.periodindex;
            """;
        IEnumerable<(int PeriodIndex, string Label)> rows = await connection
            .QueryAsync<(int PeriodIndex, string Label)>(
                new CommandDefinition(sql, new { ClassGroupId = classId }, cancellationToken: ct))
            .ConfigureAwait(false);
        List<(int PeriodIndex, string Label)> periods = rows.ToList();
        return SplitPeriodWindows(periods, yearStart, yearEnd);
    }

    private static IList<FeeInstallmentGenerator.PeriodWindow> SplitPeriodWindows(
        IReadOnlyList<(int PeriodIndex, string Label)> periods,
        DateOnly yearStart,
        DateOnly yearEnd)
    {
        if (periods.Count == 0)
        {
            return Array.Empty<FeeInstallmentGenerator.PeriodWindow>();
        }

        int totalDays = Math.Max(1, yearEnd.DayNumber - yearStart.DayNumber + 1);
        int count = periods.Count;
        List<FeeInstallmentGenerator.PeriodWindow> windows = new(count);
        for (int i = 0; i < count; i++)
        {
            DateOnly start = yearStart.AddDays((totalDays * i) / count);
            DateOnly end = i == count - 1
                ? yearEnd
                : yearStart.AddDays((totalDays * (i + 1) / count) - 1);
            if (end < start)
            {
                end = start;
            }

            windows.Add(new FeeInstallmentGenerator.PeriodWindow(
                periods[i].PeriodIndex,
                periods[i].Label,
                start,
                end));
        }

        return windows;
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
