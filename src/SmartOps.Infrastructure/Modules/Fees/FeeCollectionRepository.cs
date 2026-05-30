using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class FeeCollectionRepository : BaseRepository, IFeeCollectionRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    private const string ClassDisplayNameSql =
        "c.classname || CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    public FeeCollectionRepository(
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

    /// <summary>
    /// One enrollment per student for the requested year (active first; inactive for promoted/historical view).
    /// </summary>
    private static string StudentEnrollmentForYearJoin(string schema) => $"""
        INNER JOIN (
            SELECT sa.studentid,
                   sa.classid,
                   sa.rollnumber,
                   sa.feestructureversionid,
                   sa.academicyearid,
                   sa.isactive,
                   ROW_NUMBER() OVER (
                       PARTITION BY sa.studentid
                       ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
            FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
            WHERE sa.academicyearid = @AcademicYearId
        ) sa ON sa.studentid = s.id AND sa.rn = 1
        INNER JOIN {schema}.{DatabaseConfig.TableClasses} c
            ON c.id = sa.classid AND c.academicyearid = @AcademicYearId AND c.isactive = true
        """;

    public async Task<IList<FeeCollectionStudentRow>> GetStudentsAsync(
        Guid? classId,
        Guid academicYearId,
        string? search,
        string? statusFilter,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string feeTypeIncluded = StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(
            Schema, "fee_row.feetypeid", "sa.studentid", "sa.feestructureversionid");
        string sql = $"""
            SELECT s.id AS StudentId,
                   TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS StudentName,
                   COALESCE(sa.rollnumber, '') AS RollNo,
                   c.id AS ClassId,
                   {ClassDisplayNameSql} AS ClassName,
                   COALESCE(sa.feestructureversionid, '00000000-0000-0000-0000-000000000000'::uuid) AS FeeStructureVersionId,
                   COALESCE(fsv.versionnumber, 0) AS AssignedVersionNumber,
                   COALESCE(fee_totals.total_fees, 0) AS TotalFees,
                   COALESCE(paid_totals.paid, 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableStudents} s
            {StudentEnrollmentForYearJoin(Schema)}
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeeStructureVersions} fsv ON fsv.id = sa.feestructureversionid
            LEFT JOIN LATERAL (
                SELECT COALESCE(
                    NULLIF((
                        SELECT SUM(sfi.amount)
                        FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                        WHERE sfi.studentid = s.id
                          AND sfi.feestructureversionid = sa.feestructureversionid
                          AND sfi.isactive = true
                    ), 0),
                    (SELECT SUM(cfi.amount)
                     FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                     WHERE cfi.classid = sa.classid
                       AND cfi.feestructureversionid = sa.feestructureversionid
                       AND cfi.isactive = true
                       AND {StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(Schema, "cfi.feetypeid", "sa.studentid", "sa.feestructureversionid")}),
                    (SELECT SUM(cfa.amount)
                     FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                     INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                     WHERE cfa.classid = sa.classid
                       AND cfa.feestructureversionid = sa.feestructureversionid
                       AND cfa.isactive = true
                       AND {StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(Schema, "cfa.feetypeid", "sa.studentid", "sa.feestructureversionid")})
                ) AS total_fees
            ) fee_totals ON true
            LEFT JOIN LATERAL (
                SELECT SUM(fp.amount) AS paid
                FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                WHERE fp.studentid = s.id
                  AND fp.feestructureversionid = sa.feestructureversionid
                  AND fp.isactive = true
            ) paid_totals ON true
            WHERE s.isactive = true
            {(classId.HasValue ? "AND sa.classid = @ClassId" : string.Empty)}
            {(string.IsNullOrWhiteSpace(search) ? string.Empty : "AND (LOWER(s.firstname || ' ' || s.lastname) LIKE @Search OR LOWER(sa.rollnumber) LIKE @Search)")}
            ORDER BY sa.rollnumber, s.firstname;
            """;

        string? searchParam = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim().ToLowerInvariant()}%";
        IEnumerable<FeeCollectionStudentRow> rows = await connection
            .QueryAsync<FeeCollectionStudentRow>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, ClassId = classId, Search = searchParam },
                cancellationToken: ct))
            .ConfigureAwait(false);

        IList<FeeCollectionStudentRow> list = rows.ToList();
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return list;
        }

        return list.Where(r => MatchesStatusFilter(r, statusFilter)).ToList();
    }

    public async Task<FeeCollectionStudentRow?> GetStudentRowAsync(Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT s.id AS StudentId,
                   TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS StudentName,
                   COALESCE(sa.rollnumber, '') AS RollNo,
                   c.id AS ClassId,
                   {ClassDisplayNameSql} AS ClassName,
                   COALESCE(sa.feestructureversionid, '00000000-0000-0000-0000-000000000000'::uuid) AS FeeStructureVersionId,
                   COALESCE(fsv.versionnumber, 0) AS AssignedVersionNumber,
                   COALESCE(fee_totals.total_fees, 0) AS TotalFees,
                   COALESCE(paid_totals.paid, 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableStudents} s
            {StudentEnrollmentForYearJoin(Schema)}
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeeStructureVersions} fsv ON fsv.id = sa.feestructureversionid
            LEFT JOIN LATERAL (
                SELECT COALESCE(
                    NULLIF((
                        SELECT SUM(sfi.amount)
                        FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                        WHERE sfi.studentid = s.id
                          AND sfi.feestructureversionid = sa.feestructureversionid
                          AND sfi.isactive = true
                    ), 0),
                    (SELECT SUM(cfi.amount)
                     FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi
                     WHERE cfi.classid = sa.classid
                       AND cfi.feestructureversionid = sa.feestructureversionid
                       AND cfi.isactive = true
                       AND {StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(Schema, "cfi.feetypeid", "sa.studentid", "sa.feestructureversionid")}),
                    (SELECT SUM(cfa.amount)
                     FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                     INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                     WHERE cfa.classid = sa.classid
                       AND cfa.feestructureversionid = sa.feestructureversionid
                       AND cfa.isactive = true
                       AND {StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(Schema, "cfa.feetypeid", "sa.studentid", "sa.feestructureversionid")})
                ) AS total_fees
            ) fee_totals ON true
            LEFT JOIN LATERAL (
                SELECT SUM(fp.amount) AS paid
                FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                WHERE fp.studentid = s.id
                  AND fp.feestructureversionid = sa.feestructureversionid
                  AND fp.isactive = true
            ) paid_totals ON true
            WHERE s.id = @StudentId AND s.isactive = true
            LIMIT 1;
            """;

        return await connection
            .QueryFirstOrDefaultAsync<FeeCollectionStudentRow>(
                new CommandDefinition(sql, new { StudentId = studentId, AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<StudentClassFeeAmountRow>> GetStudentFeeAmountsAsync(
        Guid classId,
        Guid feeStructureVersionId,
        Guid studentId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string feeTypeIncluded = StudentFeeHeadAssignmentSql.FeeTypeIncludedPredicate(
            Schema, "ft.id", "@StudentId", "@FeeStructureVersionId");
        string sql = $"""
            SELECT ft.id AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.frequency AS CollectionType,
                   COALESCE(cfa.amount, 0) AS Amount
            FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
            INNER JOIN {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                ON cfa.feetypeid = ft.id
               AND cfa.classid = @ClassId
               AND cfa.feestructureversionid = @FeeStructureVersionId
               AND cfa.isactive = true
            WHERE ft.feestructureversionid = @FeeStructureVersionId
              AND ft.isactive = true
              AND cfa.amount > 0
              AND {feeTypeIncluded}
            ORDER BY ft.name;
            """;
        IEnumerable<StudentClassFeeAmountRow> rows = await connection
            .QueryAsync<StudentClassFeeAmountRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId, StudentId = studentId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<decimal> GetStudentTotalFeesAsync(
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(
                (SELECT SUM(amount) FROM {Schema}.{DatabaseConfig.TableClassFeeInstallments}
                 WHERE classid = @ClassId AND feestructureversionid = @FeeStructureVersionId AND isactive = true),
                (SELECT SUM(cfa.amount)
                 FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                 INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                 WHERE cfa.classid = @ClassId AND cfa.feestructureversionid = @FeeStructureVersionId AND cfa.isactive = true)
            );
            """;
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { ClassId = classId, FeeStructureVersionId = feeStructureVersionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<decimal> GetStudentPaidTotalAsync(Guid studentId, Guid feeStructureVersionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(SUM(amount), 0)
            FROM {Schema}.{DatabaseConfig.TableFeePayments}
            WHERE studentid = @StudentId
              AND feestructureversionid = @FeeStructureVersionId
              AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid?> GetStudentFeeStructureVersionHintAsync(Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT feestructureversionid
            FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
            WHERE studentid = @StudentId
              AND isactive = true
              AND feestructureversionid IS NOT NULL
              AND feestructureversionid <> '00000000-0000-0000-0000-000000000000'::uuid
            ORDER BY createdon DESC
            LIMIT 1;
            """;
        Guid? fromAssignments = await connection
            .QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
        if (fromAssignments.HasValue && fromAssignments.Value != Guid.Empty)
        {
            return fromAssignments;
        }

        sql = $"""
            SELECT feestructureversionid
            FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
            WHERE studentid = @StudentId
              AND isactive = true
              AND feestructureversionid IS NOT NULL
              AND feestructureversionid <> '00000000-0000-0000-0000-000000000000'::uuid
            ORDER BY createdon DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task AssignStudentFeeStructureVersionAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableStudentAcademics}
            SET feestructureversionid = @FeeStructureVersionId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE studentid = @StudentId
              AND academicyearid = @AcademicYearId
              AND isactive = true
              AND (feestructureversionid IS NULL OR feestructureversionid = '00000000-0000-0000-0000-000000000000'::uuid);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { StudentId = studentId, AcademicYearId = academicYearId, FeeStructureVersionId = feeStructureVersionId, UpdatedBy = actorId, UpdatedOn = utcNow },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<PriorYearEnrollmentRow?> GetLatestPriorYearEnrollmentAsync(
        Guid studentId,
        Guid targetAcademicYearId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT sa.academicyearid AS AcademicYearId,
                   sa.classid AS ClassId,
                   COALESCE(sa.feestructureversionid, '00000000-0000-0000-0000-000000000000'::uuid) AS FeeStructureVersionId
            FROM {Schema}.{DatabaseConfig.TableStudentAcademics} sa
            INNER JOIN {Schema}.{DatabaseConfig.TableAcademicYears} ay
                ON ay.id = sa.academicyearid AND ay.isactive = true
            INNER JOIN {Schema}.{DatabaseConfig.TableAcademicYears} target_ay
                ON target_ay.id = @TargetAcademicYearId AND target_ay.isactive = true
            WHERE sa.studentid = @StudentId
              AND ay.startdate < target_ay.startdate
            ORDER BY ay.startdate DESC, sa.isactive DESC, sa.createdon DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PriorYearEnrollmentRow>(
                new CommandDefinition(
                    sql,
                    new { StudentId = studentId, TargetAcademicYearId = targetAcademicYearId },
                    cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<StudentFeeHeadPaidRow>> GetPaidByFeeTypeAsync(Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT fpa.feetypeid AS FeeTypeId, COALESCE(SUM(fpa.amount), 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
            INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
            WHERE fp.studentid = @StudentId AND fpa.isactive = true
            GROUP BY fpa.feetypeid;
            """;
        IEnumerable<StudentFeeHeadPaidRow> rows = await connection
            .QueryAsync<StudentFeeHeadPaidRow>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<FeePaymentHistoryRow>> GetPaymentHistoryAsync(Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT fp.id AS PaymentId,
                   fp.paymentdate AS PaymentDate,
                   fp.paymentmode AS PaymentMode,
                   fp.amount AS Amount,
                   fp.transactionno AS TransactionNo,
                   fp.receiptno AS ReceiptNo,
                   COALESCE(string_agg(
                       CASE
                           WHEN cfi.periodlabel IS NOT NULL AND cfi.periodlabel <> ''
                           THEN ft.name || ' — ' || cfi.periodlabel
                           ELSE ft.name
                       END,
                       ', ' ORDER BY ft.name, cfi.periodindex), 'Fee collected') AS FeeHeadsSummary
            FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa ON fpa.paymentid = fp.id AND fpa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = fpa.feetypeid
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassFeeInstallments} cfi ON cfi.id = fpa.installmentid
            WHERE fp.studentid = @StudentId AND fp.isactive = true
            GROUP BY fp.id, fp.paymentdate, fp.paymentmode, fp.amount, fp.transactionno, fp.receiptno, fp.createdon
            ORDER BY fp.paymentdate DESC, fp.createdon DESC;
            """;
        IEnumerable<FeePaymentHistoryRow> rows = await connection
            .QueryAsync<FeePaymentHistoryRow>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<(Guid PaymentId, string ReceiptNo)> CreatePaymentAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        decimal amount,
        int paymentMode,
        string? transactionNo,
        DateOnly paymentDate,
        string? remarks,
        IList<(Guid FeeTypeId, Guid? InstallmentId, decimal Amount)> allocations,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            DateTime utcNow = DateTime.UtcNow;
            Guid actorId = ResolveInsertActor();
            Guid paymentId = Guid.NewGuid();
            string receiptNo = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{paymentId.ToString()[..8].ToUpperInvariant()}";

        var payment = new FeePaymentEntity
        {
            Id = paymentId,
            StudentId = studentId,
            FeeStructureVersionId = feeStructureVersionId,
            Amount = amount,
                PaymentMode = (FeePaymentMode)paymentMode,
                TransactionNo = transactionNo,
                PaymentDate = paymentDate,
                Remarks = remarks,
                ReceiptNo = receiptNo
            };
            EnsureInsertAudit(payment, utcNow, actorId);

            string paymentSql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableFeePayments}
                    (id, studentid, feestructureversionid, amount, paymentmode, transactionno, paymentdate, remarks, receiptno,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @StudentId, @FeeStructureVersionId, @Amount, @PaymentMode, @TransactionNo, @PaymentDate, @Remarks, @ReceiptNo,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await connection.ExecuteAsync(
                new CommandDefinition(paymentSql, payment, transaction, cancellationToken: ct)).ConfigureAwait(false);

            foreach ((Guid feeTypeId, Guid? installmentId, decimal allocAmount) in allocations.Where(a => a.Amount > 0))
            {
                var alloc = new FeePaymentAllocationEntity
                {
                    Id = Guid.NewGuid(),
                    PaymentId = paymentId,
                    FeeTypeId = feeTypeId,
                    InstallmentId = installmentId,
                    Amount = allocAmount
                };
                EnsureInsertAudit(alloc, utcNow, actorId);
                string allocSql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableFeePaymentAllocations}
                        (id, paymentid, feetypeid, installmentid, amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @PaymentId, @FeeTypeId, @InstallmentId, @Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """;
                await connection.ExecuteAsync(
                    new CommandDefinition(allocSql, alloc, transaction, cancellationToken: ct)).ConfigureAwait(false);
            }

            transaction.Commit();
            return (paymentId, receiptNo);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool MatchesStatusFilter(FeeCollectionStudentRow row, string statusFilter)
    {
        decimal due = Math.Max(0, row.TotalFees - row.PaidAmount);
        return statusFilter.ToLowerInvariant() switch
        {
            "paid" or "fully paid" => due <= 0 && row.TotalFees > 0,
            "partial" => row.PaidAmount > 0 && due > 0,
            "overdue" or "unpaid" => row.PaidAmount <= 0 && row.TotalFees > 0,
            _ => true
        };
    }
}
