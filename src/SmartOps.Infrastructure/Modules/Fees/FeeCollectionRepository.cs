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

    public async Task<IList<FeeCollectionStudentRow>> GetStudentsAsync(
        Guid? classId,
        Guid academicYearId,
        string? search,
        string? statusFilter,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT s.id AS StudentId,
                   TRIM(COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '')) AS StudentName,
                   COALESCE(sa.rollnumber, '') AS RollNo,
                   c.id AS ClassId,
                   {ClassDisplayNameSql} AS ClassName,
                   COALESCE(fee_totals.total_fees, 0) AS TotalFees,
                   COALESCE(paid_totals.paid, 0) AS PaidAmount
            FROM {Schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {Schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id AND sa.isactive = true
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid AND c.isactive = true
            LEFT JOIN LATERAL (
                SELECT SUM(cfa.amount) AS total_fees
                FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                WHERE cfa.classid = sa.classid AND cfa.academicyearid = @AcademicYearId AND cfa.isactive = true
            ) fee_totals ON true
            LEFT JOIN LATERAL (
                SELECT SUM(fp.amount) AS paid
                FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                WHERE fp.studentid = s.id AND fp.isactive = true
            ) paid_totals ON true
            WHERE s.isactive = true AND sa.academicyearid = @AcademicYearId
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
        IList<FeeCollectionStudentRow> rows = await GetStudentsAsync(null, academicYearId, null, null, ct).ConfigureAwait(false);
        return rows.FirstOrDefault(r => r.StudentId == studentId);
    }

    public async Task<IList<StudentClassFeeAmountRow>> GetStudentFeeAmountsAsync(
        Guid studentId,
        Guid classId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.frequency AS Frequency,
                   COALESCE(cfa.amount, 0) AS Amount
            FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                ON cfa.feetypeid = ft.id AND cfa.classid = @ClassId AND cfa.academicyearid = @AcademicYearId AND cfa.isactive = true
            WHERE ft.isactive = true
            ORDER BY ft.name;
            """;
        IEnumerable<StudentClassFeeAmountRow> rows = await connection
            .QueryAsync<StudentClassFeeAmountRow>(new CommandDefinition(
                sql,
                new { ClassId = classId, AcademicYearId = academicYearId, StudentId = studentId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<decimal> GetStudentPaidTotalAsync(Guid studentId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(SUM(amount), 0)
            FROM {Schema}.{DatabaseConfig.TableFeePayments}
            WHERE studentid = @StudentId AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
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
                   COALESCE(string_agg(ft.name, ', ' ORDER BY ft.name), 'Fee collected') AS FeeHeadsSummary
            FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa ON fpa.paymentid = fp.id AND fpa.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = fpa.feetypeid
            WHERE fp.studentid = @StudentId AND fp.isactive = true
            GROUP BY fp.id, fp.paymentdate, fp.paymentmode, fp.amount, fp.transactionno, fp.receiptno
            ORDER BY fp.paymentdate DESC, fp.createdon DESC;
            """;
        IEnumerable<FeePaymentHistoryRow> rows = await connection
            .QueryAsync<FeePaymentHistoryRow>(new CommandDefinition(sql, new { StudentId = studentId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<(Guid PaymentId, string ReceiptNo)> CreatePaymentAsync(
        Guid studentId,
        decimal amount,
        int paymentMode,
        string? transactionNo,
        DateOnly paymentDate,
        string? remarks,
        IList<(Guid FeeTypeId, decimal Amount)> allocations,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        Guid paymentId = Guid.NewGuid();
        string receiptNo = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{paymentId.ToString()[..8].ToUpperInvariant()}";

        var payment = new FeePaymentEntity
        {
            Id = paymentId,
            StudentId = studentId,
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
                (id, studentid, amount, paymentmode, transactionno, paymentdate, remarks, receiptno,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @StudentId, @Amount, @PaymentMode, @TransactionNo, @PaymentDate, @Remarks, @ReceiptNo,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(paymentSql, payment, cancellationToken: ct)).ConfigureAwait(false);

        foreach ((Guid feeTypeId, decimal allocAmount) in allocations.Where(a => a.Amount > 0))
        {
            var alloc = new FeePaymentAllocationEntity
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId,
                FeeTypeId = feeTypeId,
                Amount = allocAmount
            };
            EnsureInsertAudit(alloc, utcNow, actorId);
            string allocSql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableFeePaymentAllocations}
                    (id, paymentid, feetypeid, amount,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @PaymentId, @FeeTypeId, @Amount,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await connection.ExecuteAsync(new CommandDefinition(allocSql, alloc, cancellationToken: ct)).ConfigureAwait(false);
        }

        return (paymentId, receiptNo);
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
