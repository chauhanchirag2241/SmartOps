using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Application.Modules.Student.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Student;

public sealed class StudentFeeInstallmentRepository : BaseRepository, IStudentFeeInstallmentRepository
{
    private readonly IClassFeeInstallmentRepository _classInstallmentRepo;

    public StudentFeeInstallmentRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IClassFeeInstallmentRepository classInstallmentRepo)
        : base(context, currentUser)
    {
        _classInstallmentRepo = classInstallmentRepo;
    }

    private string Schema => Context.OperationalSchema;

    public async Task<bool> IsSchemaReadyAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @Schema
                  AND table_name = @TableName
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { Schema, TableName = DatabaseConfig.TableStudentFeeInstallments },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ClassFeeInstallmentRow>> GetByStudentVersionAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<ClassFeeInstallmentRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT sfi.id AS Id,
                   sfi.feeheadid AS FeeHeadId,
                   ft.name AS FeeHeadName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   sfi.periodindex AS PeriodIndex,
                   sfi.periodlabel AS PeriodLabel,
                   sfi.periodstart AS PeriodStart,
                   sfi.periodend AS PeriodEnd,
                   sfi.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeHead} ft ON ft.id = sfi.feeheadid AND ft.isactive = true
            WHERE sfi.studentid = @StudentId
              AND sfi.feestructureid = @FeeStructureId
              AND sfi.isactive = true
            ORDER BY ft.name, sfi.periodindex;
            """;
        IEnumerable<ClassFeeInstallmentRow> rows = await connection
            .QueryAsync<ClassFeeInstallmentRow>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> StudentHasInstallmentsAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                WHERE studentid = @StudentId
                  AND feestructureid = @FeeStructureId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> InstallmentBelongsToStudentAsync(
        Guid installmentId,
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                WHERE id = @InstallmentId
                  AND studentid = @StudentId
                  AND feestructureid = @FeeStructureId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { InstallmentId = installmentId, StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task GenerateForStudentAdmissionAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        IList<StudentFeeHeadAssignmentEntity> assignments,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        await _classInstallmentRepo
            .EnsureMissingInstallmentsForClassVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);
        IList<ClassFeeInstallmentRow> classInstallments = await _classInstallmentRepo
            .GetByClassVersionAsync(classId, feeStructureId, ct)
            .ConfigureAwait(false);

        IList<StudentFeeHeadAssignmentEntity> effectiveAssignments = assignments.Count == 0
            ? classAmounts
                .Select(a => new StudentFeeHeadAssignmentEntity
                {
                    FeeHeadId = a.FeeHeadId,
                    IsIncluded = true,
                    CustomAnnualAmount = null
                })
                .ToList()
            : assignments;

        var assignmentByFeeHead = effectiveAssignments
            .GroupBy(a => a.FeeHeadId)
            .ToDictionary(g => g.Key, g => g.First());

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                WHERE studentid = @StudentId AND feestructureid = @FeeStructureId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deactivateSql,
                new { StudentId = studentId, FeeStructureId = feeStructureId, UpdatedBy = actorId, UpdatedOn = utcNow },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            string deleteInactiveSql = $"""
                DELETE FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                WHERE sfi.studentid = @StudentId
                  AND sfi.feestructureid = @FeeStructureId
                  AND sfi.isactive = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                      WHERE fpa.installmentid = sfi.id AND fpa.isactive = true
                  );
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deleteInactiveSql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            (DateOnly yearStart, DateOnly yearEnd) = await ReadAcademicYearDatesAsync(academicYearId, conn, tx, ct)
                .ConfigureAwait(false);

            foreach (ClassFeeAmountForInstallmentRow classAmount in classAmounts)
            {
                if (!assignmentByFeeHead.TryGetValue(classAmount.FeeHeadId, out StudentFeeHeadAssignmentEntity? assignment)
                    || !assignment.IsIncluded)
                {
                    continue;
                }

                var feeCategory = (FeeCategory)classAmount.Category;
                bool isDiscount = FeeCategoryHelper.IsDiscount(feeCategory);
                decimal classAnnual = (FeeCollectionType)classAmount.CollectionType == FeeCollectionType.PeriodWise
                    ? classAmount.PeriodAmounts.Sum(p => p.Amount)
                    : classAmount.Amount;
                decimal studentAnnual = assignment.CustomAnnualAmount is > 0
                    ? assignment.CustomAnnualAmount.Value
                    : classAnnual;
                if (studentAnnual <= 0)
                {
                    continue;
                }

                decimal signedStudentAnnual = FeeCategoryHelper.SignedAnnualTotal(feeCategory, studentAnnual);
                decimal signedClassAnnual = FeeCategoryHelper.SignedAnnualTotal(feeCategory, classAnnual);

                IList<ClassFeeInstallmentRow> templatePeriods = classInstallments
                    .Where(i => i.FeeHeadId == classAmount.FeeHeadId)
                    .OrderBy(i => i.PeriodIndex)
                    .ToList();

                IList<(int PeriodIndex, string Label, DateOnly Start, DateOnly End, decimal Amount)> periodsToInsert;

                if (templatePeriods.Count > 0)
                {
                    periodsToInsert = ScaleClassPeriods(templatePeriods, signedClassAnnual, signedStudentAnnual);
                }
                else
                {
                    IList<FeeInstallmentGenerator.PeriodWindow> periodWindows = await GetPeriodWindowsAsync(
                            classId,
                            yearStart,
                            yearEnd,
                            conn,
                            tx,
                            ct)
                        .ConfigureAwait(false);
                    IList<FeeInstallmentGenerator.InstallmentPeriod> generated = FeeInstallmentGenerator.Generate(
                        (FeeCollectionType)classAmount.CollectionType,
                        classAmount.Amount,
                        classAmount.PeriodAmounts
                            .Select(p => new FeeInstallmentGenerator.PeriodAmount(p.PeriodIndex, p.Amount))
                            .ToList(),
                        periodWindows,
                        yearStart,
                        yearEnd);
                    if (studentAnnual != classAnnual && classAnnual > 0)
                    {
                        decimal ratio = studentAnnual / classAnnual;
                        generated = generated
                            .Select(p => new FeeInstallmentGenerator.InstallmentPeriod(
                                p.PeriodIndex,
                                p.PeriodLabel,
                                p.PeriodStart,
                                p.PeriodEnd,
                                Math.Round(p.Amount * ratio, 2)))
                            .ToList();
                    }

                    if (isDiscount)
                    {
                        generated = generated
                            .Select(p => new FeeInstallmentGenerator.InstallmentPeriod(
                                p.PeriodIndex,
                                p.PeriodLabel,
                                p.PeriodStart,
                                p.PeriodEnd,
                                FeeCategoryHelper.SignedInstallmentAmount(feeCategory, p.Amount)))
                            .ToList();
                    }

                    periodsToInsert = generated
                        .Select(p => (p.PeriodIndex, p.PeriodLabel, p.PeriodStart, p.PeriodEnd, p.Amount))
                        .ToList();
                }

                foreach ((int periodIndex, string label, DateOnly start, DateOnly end, decimal amount) in periodsToInsert)
                {
                    Guid? classInstId = templatePeriods.FirstOrDefault(p => p.PeriodIndex == periodIndex)?.Id;
                    var entity = new StudentFeeInstallmentEntity
                    {
                        Id = Guid.NewGuid(),
                        StudentId = studentId,
                        FeeStructureId = feeStructureId,
                        ClassFeeInstallmentId = classInstId == Guid.Empty ? null : classInstId,
                        FeeHeadId = classAmount.FeeHeadId,
                        PeriodIndex = periodIndex,
                        PeriodLabel = label,
                        PeriodStart = start,
                        PeriodEnd = end,
                        Amount = amount
                    };
                    EnsureInsertAudit(entity, utcNow, actorId);
                    string insertSql = $"""
                        INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                            (id, studentid, feestructureid, classfeeinstallmentid, feeheadid,
                             periodindex, periodlabel, periodstart, periodend, amount,
                             isactive, versionno, createdby, createdon, updatedby, updatedon)
                        VALUES
                            (@Id, @StudentId, @FeeStructureId, @ClassFeeInstallmentId, @FeeHeadId,
                             @PeriodIndex, @PeriodLabel, @PeriodStart, @PeriodEnd, @Amount,
                             @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                        """;
                    await conn.ExecuteAsync(new CommandDefinition(insertSql, entity, transaction: tx, cancellationToken: ct))
                        .ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> StudentHasInstallmentPaymentsAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS (
                SELECT 1
                FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp
                    ON fp.id = fpa.paymentid AND fp.isactive = true
                INNER JOIN {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                    ON sfi.id = fpa.installmentid AND sfi.isactive = true
                WHERE fp.studentid = @StudentId
                  AND fp.feestructureid = @FeeStructureId
                  AND fpa.isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> InstallmentsAlignWithAssignmentsAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return true;
        }

        if (!await StudentHasInstallmentsAsync(studentId, feeStructureId, ct).ConfigureAwait(false))
        {
            return false;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureId,
                classId,
                academicYearId,
                ct)
            .ConfigureAwait(false);
        HashSet<Guid> expectedFeeHeads = assignments
            .Where(a => a.IsIncluded)
            .Select(a => a.FeeHeadId)
            .ToHashSet();

        IList<ClassFeeInstallmentRow> rows = await GetByStudentVersionAsync(studentId, feeStructureId, ct)
            .ConfigureAwait(false);
        HashSet<Guid> actualFeeHeads = rows.Select(r => r.FeeHeadId).ToHashSet();

        return expectedFeeHeads.SetEquals(actualFeeHeads);
    }

    public async Task EnsureForStudentAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await StudentHasInstallmentPaymentsAsync(studentId, feeStructureId, ct).ConfigureAwait(false))
        {
            return;
        }

        if (await HasCurrentYearFeeInstallmentsAsync(studentId, feeStructureId, ct).ConfigureAwait(false)
            && await InstallmentsAlignWithAssignmentsAsync(studentId, classId, feeStructureId, academicYearId, ct)
                .ConfigureAwait(false))
        {
            return;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureId,
                classId,
                academicYearId,
                ct)
            .ConfigureAwait(false);

        await GenerateForStudentAdmissionAsync(
            studentId,
            classId,
            feeStructureId,
            academicYearId,
            assignments,
            ct).ConfigureAwait(false);
    }

    public const string CarriedForwardPeriodLabel = "Previous year pending";

    public static bool IsCarriedForwardPeriodLabel(string? periodLabel) =>
        string.Equals(periodLabel, CarriedForwardPeriodLabel, StringComparison.OrdinalIgnoreCase);

    public async Task<bool> HasCurrentYearFeeInstallmentsAsync(
        Guid studentId,
        Guid feeStructureId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IList<ClassFeeInstallmentRow> rows = await GetByStudentVersionAsync(studentId, feeStructureId, ct)
            .ConfigureAwait(false);
        return rows.Any(r => !IsCarriedForwardPeriodLabel(r.PeriodLabel));
    }

    public async Task EnsureCurrentYearInstallmentsAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await StudentHasInstallmentPaymentsAsync(studentId, feeStructureId, ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByStudentVersionAsync(studentId, feeStructureId, ct)
            .ConfigureAwait(false);
        decimal carriedForward = existing
            .Where(r => IsCarriedForwardPeriodLabel(r.PeriodLabel))
            .Sum(r => r.Amount);

        if (await HasCurrentYearFeeInstallmentsAsync(studentId, feeStructureId, ct).ConfigureAwait(false)
            && await InstallmentsAlignWithAssignmentsAsync(studentId, classId, feeStructureId, academicYearId, ct)
                .ConfigureAwait(false))
        {
            return;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsForGenerationAsync(
                studentId,
                classId,
                feeStructureId,
                academicYearId,
                ct)
            .ConfigureAwait(false);

        await GenerateForStudentAdmissionAsync(
                studentId,
                classId,
                feeStructureId,
                academicYearId,
                assignments,
                ct)
            .ConfigureAwait(false);

        if (carriedForward > 0)
        {
            await AddCarriedForwardBalanceAsync(
                    studentId,
                    classId,
                    feeStructureId,
                    academicYearId,
                    carriedForward,
                    ct)
                .ConfigureAwait(false);
        }
    }

    private async Task<IList<StudentFeeHeadAssignmentEntity>> LoadAssignmentsForGenerationAsync(
        Guid studentId,
        Guid feeStructureId,
        Guid classId,
        Guid academicYearId,
        CancellationToken ct)
    {
        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureId,
                classId,
                academicYearId,
                ct)
            .ConfigureAwait(false);

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);

        if (classAmounts.Count == 0)
        {
            return assignments;
        }

        var byFeeHead = assignments
            .GroupBy(a => a.FeeHeadId)
            .ToDictionary(g => g.Key, g => g.First());

        return classAmounts
            .Select(ca =>
            {
                if (byFeeHead.TryGetValue(ca.FeeHeadId, out StudentFeeHeadAssignmentEntity? existing))
                {
                    return existing;
                }

                return new StudentFeeHeadAssignmentEntity
                {
                    FeeHeadId = ca.FeeHeadId,
                    IsIncluded = true,
                    CustomAnnualAmount = null
                };
            })
            .ToList();
    }

    public async Task CopyFeeHeadAssignmentsFromVersionAsync(
        Guid studentId,
        Guid sourceFeeStructureId,
        Guid targetFeeStructureId,
        CancellationToken ct = default)
    {
        if (sourceFeeStructureId == targetFeeStructureId
            || sourceFeeStructureId == Guid.Empty
            || targetFeeStructureId == Guid.Empty)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
                (id, studentid, feestructureid, feeheadid, isincluded, customannualamount,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            SELECT gen_random_uuid(),
                   src.studentid,
                   @TargetVersionId,
                   src.feeheadid,
                   src.isincluded,
                   src.customannualamount,
                   true,
                   1,
                   @CreatedBy,
                   @CreatedOn,
                   @UpdatedBy,
                   @UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments} src
            WHERE src.studentid = @StudentId
              AND src.feestructureid = @SourceVersionId
              AND src.isactive = true
              AND NOT EXISTS (
                  SELECT 1
                  FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments} tgt
                  WHERE tgt.studentid = src.studentid
                    AND tgt.feestructureid = @TargetVersionId
                    AND tgt.feeheadid = src.feeheadid
                    AND tgt.isactive = true);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                StudentId = studentId,
                SourceVersionId = sourceFeeStructureId,
                TargetVersionId = targetFeeStructureId,
                CreatedBy = actorId,
                CreatedOn = utcNow,
                UpdatedBy = actorId,
                UpdatedOn = utcNow,
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task AddCarriedForwardBalanceAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureId,
        Guid academicYearId,
        decimal pendingAmount,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false)
            || pendingAmount <= 0
            || feeStructureId == Guid.Empty)
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByStudentVersionAsync(studentId, feeStructureId, ct)
            .ConfigureAwait(false);
        if (existing.Any(i => IsCarriedForwardPeriodLabel(i.PeriodLabel)))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);
        ClassFeeAmountForInstallmentRow? feeHead = classAmounts
            .FirstOrDefault(a =>
                (a.Amount > 0 || a.PeriodAmounts.Any(p => p.Amount > 0))
                && !FeeCategoryHelper.IsDiscount((FeeCategory)a.Category));
        if (feeHead is null)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (DateOnly yearStart, DateOnly yearEnd) = await ReadAcademicYearDatesStandaloneAsync(academicYearId, connection, ct)
            .ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        var entity = new StudentFeeInstallmentEntity
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            FeeStructureId = feeStructureId,
            ClassFeeInstallmentId = null,
            FeeHeadId = feeHead.FeeHeadId,
            PeriodIndex = 0,
            PeriodLabel = CarriedForwardPeriodLabel,
            PeriodStart = yearStart,
            PeriodEnd = yearEnd,
            Amount = Math.Round(pendingAmount, 2, MidpointRounding.AwayFromZero),
        };
        EnsureInsertAudit(entity, utcNow, actorId);
        string insertSql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                (id, studentid, feestructureid, classfeeinstallmentid, feeheadid,
                 periodindex, periodlabel, periodstart, periodend, amount,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @StudentId, @FeeStructureId, @ClassFeeInstallmentId, @FeeHeadId,
                 @PeriodIndex, @PeriodLabel, @PeriodStart, @PeriodEnd, @Amount,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertSql, entity, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task<(DateOnly Start, DateOnly End)> ReadAcademicYearDatesStandaloneAsync(
        Guid academicYearId,
        IDbConnection connection,
        CancellationToken ct)
    {
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

    private async Task<IList<StudentFeeHeadAssignmentEntity>> LoadAssignmentsAsync(
        Guid studentId,
        Guid feeStructureId,
        Guid classId,
        Guid academicYearId,
        CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT feeheadid AS FeeHeadId, isincluded AS IsIncluded, customannualamount AS CustomAnnualAmount
            FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
            WHERE studentid = @StudentId
              AND feestructureid = @FeeStructureId
              AND isactive = true;
            """;
        IList<StudentFeeHeadAssignmentEntity> assignments = (await connection
            .QueryAsync<StudentFeeHeadAssignmentEntity>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();

        if (assignments.Count > 0)
        {
            return assignments;
        }

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureId, academicYearId, ct)
            .ConfigureAwait(false);
        return classAmounts
            .Select(a => new StudentFeeHeadAssignmentEntity
            {
                FeeHeadId = a.FeeHeadId,
                IsIncluded = true,
                CustomAnnualAmount = null
            })
            .ToList();
    }

    private static IList<(int PeriodIndex, string Label, DateOnly Start, DateOnly End, decimal Amount)> ScaleClassPeriods(
        IList<ClassFeeInstallmentRow> templatePeriods,
        decimal classAnnual,
        decimal studentAnnual)
    {
        if (templatePeriods.Count == 0)
        {
            return Array.Empty<(int, string, DateOnly, DateOnly, decimal)>();
        }

        if (classAnnual <= 0)
        {
            decimal even = Math.Round(studentAnnual / templatePeriods.Count, 2, MidpointRounding.AwayFromZero);
            decimal assigned = 0m;
            var evenResult = new List<(int, string, DateOnly, DateOnly, decimal)>(templatePeriods.Count);
            for (int i = 0; i < templatePeriods.Count; i++)
            {
                ClassFeeInstallmentRow row = templatePeriods[i];
                decimal amount = i == templatePeriods.Count - 1 ? studentAnnual - assigned : even;
                assigned += amount;
                evenResult.Add((row.PeriodIndex, row.PeriodLabel, row.PeriodStart, row.PeriodEnd, amount));
            }

            return evenResult;
        }

        decimal scale = studentAnnual / classAnnual;
        decimal totalAssigned = 0m;
        var result = new List<(int, string, DateOnly, DateOnly, decimal)>(templatePeriods.Count);
        for (int i = 0; i < templatePeriods.Count; i++)
        {
            ClassFeeInstallmentRow row = templatePeriods[i];
            decimal amount = i == templatePeriods.Count - 1
                ? studentAnnual - totalAssigned
                : Math.Round(row.Amount * scale, 2, MidpointRounding.AwayFromZero);
            totalAssigned += amount;
            result.Add((row.PeriodIndex, row.PeriodLabel, row.PeriodStart, row.PeriodEnd, amount));
        }

        return result;
    }

    private async Task<(DateOnly Start, DateOnly End)> ReadAcademicYearDatesAsync(
        Guid academicYearId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT startdate AS StartDate, enddate AS EndDate
            FROM {Schema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """;
        var row = await conn.QueryFirstOrDefaultAsync<(DateOnly StartDate, DateOnly EndDate)>(
            new CommandDefinition(sql, new { Id = academicYearId }, transaction: tx, cancellationToken: ct))
            .ConfigureAwait(false);
        if (row.StartDate == default || row.EndDate == default)
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            return (today, today.AddMonths(11));
        }

        return (row.StartDate, row.EndDate);
    }

    private async Task<IList<FeeInstallmentGenerator.PeriodWindow>> GetPeriodWindowsAsync(
        Guid classId,
        DateOnly yearStart,
        DateOnly yearEnd,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT periodindex AS PeriodIndex,
                   name AS Label
            FROM {Schema}.{DatabaseConfig.TableClassAcademicPeriods}
            WHERE classgroupid = @ClassGroupId AND isactive = true
            ORDER BY periodindex;
            """;
        IEnumerable<(int PeriodIndex, string Label)> rows = await conn
            .QueryAsync<(int PeriodIndex, string Label)>(
                new CommandDefinition(sql, new { ClassGroupId = classId }, transaction: tx, cancellationToken: ct))
            .ConfigureAwait(false);
        List<(int PeriodIndex, string Label)> periods = rows.ToList();
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
}
