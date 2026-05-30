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
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return Array.Empty<ClassFeeInstallmentRow>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT sfi.id AS Id,
                   sfi.feetypeid AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   sfi.periodindex AS PeriodIndex,
                   sfi.periodlabel AS PeriodLabel,
                   sfi.periodstart AS PeriodStart,
                   sfi.periodend AS PeriodEnd,
                   sfi.amount AS Amount
            FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
            INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = sfi.feetypeid AND ft.isactive = true
            WHERE sfi.studentid = @StudentId
              AND sfi.feestructureversionid = @FeeStructureVersionId
              AND sfi.isactive = true
            ORDER BY ft.name, sfi.periodindex;
            """;
        IEnumerable<ClassFeeInstallmentRow> rows = await connection
            .QueryAsync<ClassFeeInstallmentRow>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> StudentHasInstallmentsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
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
                  AND feestructureversionid = @FeeStructureVersionId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> InstallmentBelongsToStudentAsync(
        Guid installmentId,
        Guid studentId,
        Guid feeStructureVersionId,
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
                  AND feestructureversionid = @FeeStructureVersionId
                  AND isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { InstallmentId = installmentId, StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task GenerateForStudentAdmissionAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        IList<StudentFeeHeadAssignmentEntity> assignments,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        await _classInstallmentRepo
            .EnsureMissingInstallmentsForClassVersionAsync(classId, feeStructureVersionId, academicYearId, ct)
            .ConfigureAwait(false);

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        IList<ClassFeeInstallmentRow> classInstallments = await _classInstallmentRepo
            .GetByClassVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);

        IList<StudentFeeHeadAssignmentEntity> effectiveAssignments = assignments.Count == 0
            ? classAmounts
                .Select(a => new StudentFeeHeadAssignmentEntity
                {
                    FeeTypeId = a.FeeTypeId,
                    IsIncluded = true,
                    CustomAnnualAmount = null
                })
                .ToList()
            : assignments;

        var assignmentByFeeType = effectiveAssignments
            .GroupBy(a => a.FeeTypeId)
            .ToDictionary(g => g.Key, g => g.First());

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                WHERE studentid = @StudentId AND feestructureversionid = @FeeStructureVersionId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deactivateSql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId, UpdatedBy = actorId, UpdatedOn = utcNow },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            string deleteInactiveSql = $"""
                DELETE FROM {Schema}.{DatabaseConfig.TableStudentFeeInstallments} sfi
                WHERE sfi.studentid = @StudentId
                  AND sfi.feestructureversionid = @FeeStructureVersionId
                  AND sfi.isactive = false
                  AND NOT EXISTS (
                      SELECT 1
                      FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                      WHERE fpa.installmentid = sfi.id AND fpa.isactive = true
                  );
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                deleteInactiveSql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);

            (DateOnly yearStart, DateOnly yearEnd) = await ReadAcademicYearDatesAsync(academicYearId, conn, tx, ct)
                .ConfigureAwait(false);

            foreach (ClassFeeAmountForInstallmentRow classAmount in classAmounts)
            {
                if (!assignmentByFeeType.TryGetValue(classAmount.FeeTypeId, out StudentFeeHeadAssignmentEntity? assignment)
                    || !assignment.IsIncluded)
                {
                    continue;
                }

                var feeCategory = (FeeCategory)classAmount.Category;
                bool isDiscount = FeeCategoryHelper.IsDiscount(feeCategory);
                decimal classAnnual = (FeeCollectionType)classAmount.CollectionType == FeeCollectionType.SemesterWise
                    ? classAmount.Semester1Amount + classAmount.Semester2Amount
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
                    .Where(i => i.FeeTypeId == classAmount.FeeTypeId)
                    .OrderBy(i => i.PeriodIndex)
                    .ToList();

                IList<(int PeriodIndex, string Label, DateOnly Start, DateOnly End, decimal Amount)> periodsToInsert;

                if (templatePeriods.Count > 0)
                {
                    periodsToInsert = ScaleClassPeriods(templatePeriods, signedClassAnnual, signedStudentAnnual);
                }
                else
                {
                    IList<FeeInstallmentGenerator.SemesterWindow> semesters = await GetSemesterWindowsAsync(
                            academicYearId,
                            conn,
                            tx,
                            ct)
                        .ConfigureAwait(false);
                    IList<FeeInstallmentGenerator.InstallmentPeriod> generated = FeeInstallmentGenerator.Generate(
                        (FeeCollectionType)classAmount.CollectionType,
                        classAmount.Amount,
                        classAmount.Semester1Amount,
                        classAmount.Semester2Amount,
                        semesters,
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
                        FeeStructureVersionId = feeStructureVersionId,
                        ClassFeeInstallmentId = classInstId == Guid.Empty ? null : classInstId,
                        FeeTypeId = classAmount.FeeTypeId,
                        PeriodIndex = periodIndex,
                        PeriodLabel = label,
                        PeriodStart = start,
                        PeriodEnd = end,
                        Amount = amount
                    };
                    EnsureInsertAudit(entity, utcNow, actorId);
                    string insertSql = $"""
                        INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                            (id, studentid, feestructureversionid, classfeeinstallmentid, feetypeid,
                             periodindex, periodlabel, periodstart, periodend, amount,
                             isactive, versionno, createdby, createdon, updatedby, updatedon)
                        VALUES
                            (@Id, @StudentId, @FeeStructureVersionId, @ClassFeeInstallmentId, @FeeTypeId,
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
        Guid feeStructureVersionId,
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
                  AND fp.feestructureversionid = @FeeStructureVersionId
                  AND fpa.isactive = true
            );
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> InstallmentsAlignWithAssignmentsAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return true;
        }

        if (!await StudentHasInstallmentsAsync(studentId, feeStructureVersionId, ct).ConfigureAwait(false))
        {
            return false;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureVersionId,
                classId,
                ct)
            .ConfigureAwait(false);
        HashSet<Guid> expectedFeeTypes = assignments
            .Where(a => a.IsIncluded)
            .Select(a => a.FeeTypeId)
            .ToHashSet();

        IList<ClassFeeInstallmentRow> rows = await GetByStudentVersionAsync(studentId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        HashSet<Guid> actualFeeTypes = rows.Select(r => r.FeeTypeId).ToHashSet();

        return expectedFeeTypes.SetEquals(actualFeeTypes);
    }

    public async Task EnsureForStudentAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await StudentHasInstallmentPaymentsAsync(studentId, feeStructureVersionId, ct).ConfigureAwait(false))
        {
            return;
        }

        if (await HasCurrentYearFeeInstallmentsAsync(studentId, feeStructureVersionId, ct).ConfigureAwait(false)
            && await InstallmentsAlignWithAssignmentsAsync(studentId, classId, feeStructureVersionId, ct)
                .ConfigureAwait(false))
        {
            return;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureVersionId,
                classId,
                ct)
            .ConfigureAwait(false);

        await GenerateForStudentAdmissionAsync(
            studentId,
            classId,
            feeStructureVersionId,
            academicYearId,
            assignments,
            ct).ConfigureAwait(false);
    }

    public const string CarriedForwardPeriodLabel = "Previous year pending";

    public static bool IsCarriedForwardPeriodLabel(string? periodLabel) =>
        string.Equals(periodLabel, CarriedForwardPeriodLabel, StringComparison.OrdinalIgnoreCase);

    public async Task<bool> HasCurrentYearFeeInstallmentsAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return false;
        }

        IList<ClassFeeInstallmentRow> rows = await GetByStudentVersionAsync(studentId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        return rows.Any(r => !IsCarriedForwardPeriodLabel(r.PeriodLabel));
    }

    public async Task EnsureCurrentYearInstallmentsAsync(
        Guid studentId,
        Guid classId,
        Guid feeStructureVersionId,
        Guid academicYearId,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (await StudentHasInstallmentPaymentsAsync(studentId, feeStructureVersionId, ct).ConfigureAwait(false))
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByStudentVersionAsync(studentId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        decimal carriedForward = existing
            .Where(r => IsCarriedForwardPeriodLabel(r.PeriodLabel))
            .Sum(r => r.Amount);

        if (await HasCurrentYearFeeInstallmentsAsync(studentId, feeStructureVersionId, ct).ConfigureAwait(false)
            && await InstallmentsAlignWithAssignmentsAsync(studentId, classId, feeStructureVersionId, ct)
                .ConfigureAwait(false))
        {
            return;
        }

        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsForGenerationAsync(
                studentId,
                classId,
                feeStructureVersionId,
                ct)
            .ConfigureAwait(false);

        await GenerateForStudentAdmissionAsync(
                studentId,
                classId,
                feeStructureVersionId,
                academicYearId,
                assignments,
                ct)
            .ConfigureAwait(false);

        if (carriedForward > 0)
        {
            await AddCarriedForwardBalanceAsync(
                    studentId,
                    classId,
                    feeStructureVersionId,
                    academicYearId,
                    carriedForward,
                    ct)
                .ConfigureAwait(false);
        }
    }

    private async Task<IList<StudentFeeHeadAssignmentEntity>> LoadAssignmentsForGenerationAsync(
        Guid studentId,
        Guid feeStructureVersionId,
        Guid classId,
        CancellationToken ct)
    {
        IList<StudentFeeHeadAssignmentEntity> assignments = await LoadAssignmentsAsync(
                studentId,
                feeStructureVersionId,
                classId,
                ct)
            .ConfigureAwait(false);

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);

        if (classAmounts.Count == 0)
        {
            return assignments;
        }

        var byFeeType = assignments
            .GroupBy(a => a.FeeTypeId)
            .ToDictionary(g => g.Key, g => g.First());

        return classAmounts
            .Select(ca =>
            {
                if (byFeeType.TryGetValue(ca.FeeTypeId, out StudentFeeHeadAssignmentEntity? existing))
                {
                    return existing;
                }

                return new StudentFeeHeadAssignmentEntity
                {
                    FeeTypeId = ca.FeeTypeId,
                    IsIncluded = true,
                    CustomAnnualAmount = null
                };
            })
            .ToList();
    }

    public async Task CopyFeeHeadAssignmentsFromVersionAsync(
        Guid studentId,
        Guid sourceFeeStructureVersionId,
        Guid targetFeeStructureVersionId,
        CancellationToken ct = default)
    {
        if (sourceFeeStructureVersionId == targetFeeStructureVersionId
            || sourceFeeStructureVersionId == Guid.Empty
            || targetFeeStructureVersionId == Guid.Empty)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
                (id, studentid, feestructureversionid, feetypeid, isincluded, customannualamount,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            SELECT gen_random_uuid(),
                   src.studentid,
                   @TargetVersionId,
                   src.feetypeid,
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
              AND src.feestructureversionid = @SourceVersionId
              AND src.isactive = true
              AND NOT EXISTS (
                  SELECT 1
                  FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments} tgt
                  WHERE tgt.studentid = src.studentid
                    AND tgt.feestructureversionid = @TargetVersionId
                    AND tgt.feetypeid = src.feetypeid
                    AND tgt.isactive = true);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                StudentId = studentId,
                SourceVersionId = sourceFeeStructureVersionId,
                TargetVersionId = targetFeeStructureVersionId,
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
        Guid feeStructureVersionId,
        Guid academicYearId,
        decimal pendingAmount,
        CancellationToken ct = default)
    {
        if (!await IsSchemaReadyAsync(ct).ConfigureAwait(false)
            || pendingAmount <= 0
            || feeStructureVersionId == Guid.Empty)
        {
            return;
        }

        IList<ClassFeeInstallmentRow> existing = await GetByStudentVersionAsync(studentId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        if (existing.Any(i => IsCarriedForwardPeriodLabel(i.PeriodLabel)))
        {
            return;
        }

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        ClassFeeAmountForInstallmentRow? feeType = classAmounts
            .FirstOrDefault(a => a.Amount > 0 && !FeeCategoryHelper.IsDiscount((FeeCategory)a.Category));
        if (feeType is null)
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
            FeeStructureVersionId = feeStructureVersionId,
            ClassFeeInstallmentId = null,
            FeeTypeId = feeType.FeeTypeId,
            PeriodIndex = 0,
            PeriodLabel = CarriedForwardPeriodLabel,
            PeriodStart = yearStart,
            PeriodEnd = yearEnd,
            Amount = Math.Round(pendingAmount, 2, MidpointRounding.AwayFromZero),
        };
        EnsureInsertAudit(entity, utcNow, actorId);
        string insertSql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableStudentFeeInstallments}
                (id, studentid, feestructureversionid, classfeeinstallmentid, feetypeid,
                 periodindex, periodlabel, periodstart, periodend, amount,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @StudentId, @FeeStructureVersionId, @ClassFeeInstallmentId, @FeeTypeId,
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
        Guid feeStructureVersionId,
        Guid classId,
        CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT feetypeid AS FeeTypeId, isincluded AS IsIncluded, customannualamount AS CustomAnnualAmount
            FROM {Schema}.{DatabaseConfig.TableStudentFeeHeadAssignments}
            WHERE studentid = @StudentId
              AND feestructureversionid = @FeeStructureVersionId
              AND isactive = true;
            """;
        IList<StudentFeeHeadAssignmentEntity> assignments = (await connection
            .QueryAsync<StudentFeeHeadAssignmentEntity>(new CommandDefinition(
                sql,
                new { StudentId = studentId, FeeStructureVersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false)).ToList();

        if (assignments.Count > 0)
        {
            return assignments;
        }

        IList<ClassFeeAmountForInstallmentRow> classAmounts = await _classInstallmentRepo
            .GetClassAmountsForVersionAsync(classId, feeStructureVersionId, ct)
            .ConfigureAwait(false);
        return classAmounts
            .Select(a => new StudentFeeHeadAssignmentEntity
            {
                FeeTypeId = a.FeeTypeId,
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

    private async Task<IList<FeeInstallmentGenerator.SemesterWindow>> GetSemesterWindowsAsync(
        Guid academicYearId,
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT name AS Label, startdate AS Start, enddate AS End
            FROM {Schema}.{DatabaseConfig.TableAcademicYearSemesters}
            WHERE academicyearid = @AcademicYearId AND isactive = true
            ORDER BY semesterindex;
            """;
        IEnumerable<(string Label, DateOnly Start, DateOnly End)> rows = await conn
            .QueryAsync<(string Label, DateOnly Start, DateOnly End)>(
                new CommandDefinition(sql, new { AcademicYearId = academicYearId }, transaction: tx, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.Select(r => new FeeInstallmentGenerator.SemesterWindow(r.Label, r.Start, r.End)).ToList();
    }
}
