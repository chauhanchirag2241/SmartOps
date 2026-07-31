using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.FeeMaster;
using SmartOps.Domain.Modules.FeeMaster.Entities;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.FeeMaster;

public sealed class FeePaymentRepository : BaseRepository, IFeePaymentRepository
{
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;
    private readonly IFeeStudentAmountRepository _studentAmounts;

    public FeePaymentRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite,
        IFeeStudentAmountRepository studentAmounts)
        : base(context, currentUser)
    {
        _scope = scope;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
        _studentAmounts = studentAmounts;
    }

    public async Task<bool> HasPaymentAsync(
        Guid studentId,
        Guid feeMasterId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM {Context.OperationalSchema}.{DatabaseConfig.TableFeePayment}
                WHERE studentid = @StudentId
                  AND feemasterid = @FeeMasterId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { StudentId = studentId, FeeMasterId = feeMasterId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPaidByHeadAsync(
        Guid studentId,
        Guid feeMasterId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var sql = $"""
            SELECT l.feeheadid AS FeeHeadId, COALESCE(SUM(l.paidamount), 0) AS Paid
            FROM {schema}.{DatabaseConfig.TableFeePaymentLine} l
            INNER JOIN {schema}.{DatabaseConfig.TableFeePayment} p ON p.id = l.feepaymentid
            WHERE p.studentid = @StudentId
              AND p.feemasterid = @FeeMasterId
              AND p.isactive = true
              AND l.isactive = true
            GROUP BY l.feeheadid;
            """;
        var rows = await connection.QueryAsync<(Guid FeeHeadId, decimal Paid)>(
            new CommandDefinition(sql, new { StudentId = studentId, FeeMasterId = feeMasterId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows.ToDictionary(r => r.FeeHeadId, r => r.Paid);
    }

    public async Task<Guid> CreatePaymentAsync(
        FeePaymentEntity payment,
        IReadOnlyList<FeePaymentLineEntity> lines,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        payment.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(payment.BranchId, cancellationToken)
            .ConfigureAwait(false);

        if (payment.Id == Guid.Empty)
        {
            payment.Id = Guid.NewGuid();
        }

        EnsureInsertAudit(payment, utcNow);
        payment.PaymentDate = payment.PaymentDate == default ? DateTimeOffset.UtcNow : payment.PaymentDate;

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await InsertAsync(conn, schema, DatabaseConfig.TableFeePayment, payment, tx).ConfigureAwait(false);

            foreach (var line in lines)
            {
                if (line.Id == Guid.Empty)
                {
                    line.Id = Guid.NewGuid();
                }

                line.FeePaymentId = payment.Id;
                line.BranchId = payment.BranchId;
                EnsureInsertAudit(line, utcNow);
                await InsertAsync(conn, schema, DatabaseConfig.TableFeePaymentLine, line, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);

        return payment.Id;
    }

    public async Task<FeeCollectionDetailModel?> GetStudentCollectionDetailAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var schema = Context.OperationalSchema;
        var identity = Context.IdentitySchema;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var studentSql = $"""
            SELECT
                s.id AS StudentId,
                TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) AS StudentName,
                (
                    SELECT sp.name
                    FROM {schema}.{DatabaseConfig.TableStudentParents} sp
                    WHERE sp.studentid = s.id AND sp.isactive = true
                    ORDER BY CASE WHEN lower(sp.relationtype) LIKE '%father%' THEN 0 ELSE 1 END, sp.createdon
                    LIMIT 1
                ) AS FatherName,
                COALESCE(u.mobile, '') AS Mobile,
                cg.classname AS ClassName,
                c.section AS Section,
                a.rollnumber AS RollNumber,
                s.admissionno AS AdmissionNo
            FROM {schema}.{DatabaseConfig.TableStudents} s
            INNER JOIN {identity}.{DatabaseConfig.TableUsers} u ON u.id = s.userid
            LEFT JOIN (
                SELECT sa.studentid, sa.classid, sa.rollnumber,
                       ROW_NUMBER() OVER (PARTITION BY sa.studentid ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
                WHERE {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause("sa")}
            ) a ON a.studentid = s.id AND a.rn = 1
            LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = a.classid
            LEFT JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            WHERE s.id = @StudentId AND s.isactive = true;
            """;

        var student = await connection.QueryFirstOrDefaultAsync<FeeCollectionStudentInfo>(
            new CommandDefinition(
                studentSql,
                new { StudentId = studentId, ScopeAcademicYearId = _scope.ActiveAcademicYearId },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (student is null)
        {
            return null;
        }

        student.Initials = BuildInitials(student.StudentName);

        // ClassWise: same pool as Fee Master Students tab (enrolled in scope year),
        // then keep only fees that resolve a due amount for this student's class
        // (master class-group map, period amounts, or flat head amount).
        var mastersSql = $"""
            SELECT DISTINCT
                fm.id AS Id,
                fm.feename AS FeeName,
                fm.feetype AS FeeType,
                fm.publishedon AS PublishedOn,
                fm.defaultduedate AS DefaultDueDate,
                fm.applicableto AS ApplicableTo,
                fm.branchid AS BranchId
            FROM {schema}.{DatabaseConfig.TableFeeMaster} fm
            WHERE fm.isactive = true
              AND (
                    (
                        lower(replace(fm.applicableto, ' ', '')) = 'classwise'
                        AND EXISTS (
                            SELECT 1
                            FROM (
                                SELECT sa.studentid,
                                       sa.classid,
                                       ROW_NUMBER() OVER (
                                           PARTITION BY sa.studentid
                                           ORDER BY sa.isactive DESC, sa.createdon DESC) AS rn
                                FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
                                WHERE sa.studentid = @StudentId
                                  AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause("sa")}
                            ) enr
                            INNER JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = enr.classid
                            WHERE enr.rn = 1
                              AND EXISTS (
                                    SELECT 1
                                    FROM {schema}.{DatabaseConfig.TableFeeHead} h
                                    WHERE h.feemasterid = fm.id
                                      AND h.isactive = true
                                      AND COALESCE(
                                            h.amount,
                                            (
                                                SELECT SUM(pa.amount)
                                                FROM {schema}.{DatabaseConfig.TableFeeHeadPeriodAmount} pa
                                                WHERE pa.feeheadid = h.id
                                                  AND pa.isactive = true
                                                  AND c.classgroupid IS NOT NULL
                                                  AND pa.classgroupid = c.classgroupid
                                            ),
                                            0
                                          ) > 0
                              )
                              AND (
                                    NOT EXISTS (
                                        SELECT 1
                                        FROM {schema}.{DatabaseConfig.TableFeeMasterClassGroup} fcg
                                        WHERE fcg.feemasterid = fm.id
                                          AND fcg.isactive = true
                                    )
                                    OR (
                                        c.classgroupid IS NOT NULL
                                        AND EXISTS (
                                            SELECT 1
                                            FROM {schema}.{DatabaseConfig.TableFeeMasterClassGroup} fcg
                                            WHERE fcg.feemasterid = fm.id
                                              AND fcg.isactive = true
                                              AND fcg.classgroupid = c.classgroupid
                                        )
                                    )
                                    -- Period amounts for this class group imply applicability
                                    -- even if feemasterclassgroup row is missing/mismatched
                                    OR (
                                        c.classgroupid IS NOT NULL
                                        AND EXISTS (
                                            SELECT 1
                                            FROM {schema}.{DatabaseConfig.TableFeeHead} h2
                                            INNER JOIN {schema}.{DatabaseConfig.TableFeeHeadPeriodAmount} pa2
                                                ON pa2.feeheadid = h2.id
                                               AND pa2.isactive = true
                                            WHERE h2.feemasterid = fm.id
                                              AND h2.isactive = true
                                              AND pa2.classgroupid = c.classgroupid
                                        )
                                    )
                              )
                        )
                    )
                    OR (
                        lower(replace(fm.applicableto, ' ', '')) = 'studentwise'
                        AND EXISTS (
                            SELECT 1 FROM {schema}.{DatabaseConfig.TableFeeStudentAmount} fsa
                            WHERE fsa.feemasterid = fm.id
                              AND fsa.studentid = @StudentId
                              AND fsa.isactive = true
                        )
                    )
                  )
            ORDER BY fm.defaultduedate NULLS LAST, fm.feename;
            """;

        var masters = (await connection.QueryAsync<MasterRow>(
            new CommandDefinition(
                mastersSql,
                new
                {
                    StudentId = studentId,
                    Today = today,
                    ScopeAcademicYearId = _scope.ActiveAcademicYearId,
                },
                cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        var dueCards = new List<FeeCollectionMasterCardModel>();
        var history = new List<FeeCollectionHistoryRowModel>();
        decimal sumDue = 0, sumPaid = 0;

        foreach (var master in masters)
        {
            var detail = await _studentAmounts
                .GetStudentDetailAsync(master.Id, studentId, cancellationToken)
                .ConfigureAwait(false);
            if (detail is null)
            {
                continue;
            }

            var paidByHead = await GetPaidByHeadAsync(studentId, master.Id, cancellationToken).ConfigureAwait(false);
            var hasPayment = paidByHead.Count > 0;
            var heads = new List<FeeCollectionHeadModel>();

            foreach (var h in detail.Heads)
            {
                if (h.IsExcluded)
                {
                    continue;
                }

                var due = h.Amount ?? h.DefaultAmount ?? 0m;
                paidByHead.TryGetValue(h.FeeHeadId, out var paid);
                heads.Add(new FeeCollectionHeadModel
                {
                    FeeHeadId = h.FeeHeadId,
                    FeeHeadName = h.FeeHeadName,
                    IsMandatory = h.IsMandatory,
                    IsEditable = h.IsEditable && !hasPayment,
                    DueAmount = due,
                    PaidAmount = paid,
                    IsExcluded = false,
                });
            }

            if (heads.Count == 0)
            {
                continue;
            }

            var totalDue = heads.Sum(x => x.DueAmount);
            var totalPaid = heads.Sum(x => x.PaidAmount);
            var totalPending = Math.Max(0, totalDue - totalPaid);
            var status = ResolveStatus(totalDue, totalPaid);
            var publishedStarted = IsPublishStarted(master.PublishedOn, today);

            sumDue += totalDue;
            sumPaid += totalPaid;

            dueCards.Add(new FeeCollectionMasterCardModel
            {
                FeeMasterId = master.Id,
                FeeName = master.FeeName,
                FeeType = master.FeeType,
                PublishedOn = master.PublishedOn,
                DefaultDueDate = master.DefaultDueDate,
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                TotalPending = totalPending,
                Status = status,
                IsPublished = publishedStarted,
                CanCollect = publishedStarted && totalPending > 0,
                StudentAmountsLocked = hasPayment,
                Heads = heads,
            });

            var payments = await LoadPaymentsAsync(connection, schema, identity, studentId, master.Id, cancellationToken)
                .ConfigureAwait(false);

            history.Add(new FeeCollectionHistoryRowModel
            {
                FeeMasterId = master.Id,
                FeeName = master.FeeName,
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                TotalPending = totalPending,
                Status = status,
                Payments = payments,
            });
        }

        return new FeeCollectionDetailModel
        {
            Student = student,
            SummaryTotal = sumDue,
            SummaryPaid = sumPaid,
            SummaryPending = Math.Max(0, sumDue - sumPaid),
            DueCards = dueCards,
            History = history,
        };
    }

    private static async Task<IReadOnlyList<FeeCollectionHistoryPaymentModel>> LoadPaymentsAsync(
        System.Data.IDbConnection connection,
        string schema,
        string identity,
        Guid studentId,
        Guid feeMasterId,
        CancellationToken cancellationToken)
    {
        var paySql = $"""
            SELECT
                p.id AS PaymentId,
                p.paymentdate AS PaymentDate,
                p.totalamount AS TotalAmount,
                p.paymentmethod AS PaymentMethod,
                p.remarks AS Remarks,
                TRIM(COALESCE(u.firstname, '') || ' ' || COALESCE(u.lastname, '')) AS CollectedBy
            FROM {schema}.{DatabaseConfig.TableFeePayment} p
            LEFT JOIN {identity}.{DatabaseConfig.TableUsers} u ON u.id = p.collectedbyuserid
            WHERE p.studentid = @StudentId
              AND p.feemasterid = @FeeMasterId
              AND p.isactive = true
            ORDER BY p.paymentdate DESC;
            """;
        var payments = (await connection.QueryAsync<FeeCollectionHistoryPaymentModel>(
            new CommandDefinition(paySql, new { StudentId = studentId, FeeMasterId = feeMasterId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        if (payments.Count == 0)
        {
            return payments;
        }

        var lineSql = $"""
            SELECT
                feepaymentid AS PaymentId,
                feeheadid AS FeeHeadId,
                feeheadname AS FeeHeadName,
                dueamount AS DueAmount,
                paidamount AS PaidAmount,
                ismandatory AS IsMandatory,
                iseditable AS IsEditable
            FROM {schema}.{DatabaseConfig.TableFeePaymentLine}
            WHERE feepaymentid = ANY(@PaymentIds) AND isactive = true
            ORDER BY feeheadname;
            """;
        var paymentIds = payments.Select(p => p.PaymentId).ToArray();
        var lines = (await connection.QueryAsync<HistoryLineRow>(
            new CommandDefinition(lineSql, new { PaymentIds = paymentIds }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        var byPayment = lines.GroupBy(l => l.PaymentId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var payment in payments)
        {
            if (!byPayment.TryGetValue(payment.PaymentId, out var payLines))
            {
                payment.Lines = [];
                continue;
            }

            payment.Lines = payLines
                .Select(l => new FeeCollectionHistoryLineModel
                {
                    FeeHeadId = l.FeeHeadId,
                    FeeHeadName = l.FeeHeadName,
                    DueAmount = l.DueAmount,
                    PaidAmount = l.PaidAmount,
                    IsMandatory = l.IsMandatory,
                    IsEditable = l.IsEditable,
                })
                .ToList();
        }

        return payments;
    }

    private static bool IsPublishStarted(DateOnly? publishedOn, DateOnly today)
    {
        // No published date → treat as available to collect.
        if (!publishedOn.HasValue)
        {
            return true;
        }

        return publishedOn.Value <= today;
    }

    private static string ResolveStatus(decimal totalDue, decimal totalPaid)
    {
        if (totalPaid <= 0) return "Pending";
        if (totalPaid >= totalDue) return "Paid";
        return "Partial";
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private sealed class MasterRow
    {
        public Guid Id { get; set; }
        public string FeeName { get; set; } = string.Empty;
        public string FeeType { get; set; } = string.Empty;
        public DateOnly? PublishedOn { get; set; }
        public DateOnly? DefaultDueDate { get; set; }
        public string ApplicableTo { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
    }

    private sealed class HistoryLineRow
    {
        public Guid PaymentId { get; set; }
        public Guid FeeHeadId { get; set; }
        public string FeeHeadName { get; set; } = string.Empty;
        public decimal DueAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsEditable { get; set; }
    }
}
