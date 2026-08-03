using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Salary;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Salary;

public sealed class PayrollRepository : BaseRepository, IPayrollRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public PayrollRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    private string DepartmentExpr => SalaryDepartmentSql.DepartmentSubquery(Schema, "t");

    public async Task<PayrollRunEntity?> GetRunByYearMonthAsync(int payYear, int payMonth, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "pr", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT pr.id AS Id, pr.branchid AS BranchId, pr.payyear AS PayYear, pr.paymonth AS PayMonth, pr.status AS Status,
                   pr.useattendancewisesalary AS UseAttendanceWiseSalary,
                   pr.totalgross AS TotalGross, pr.totaldeductions AS TotalDeductions, pr.totalnet AS TotalNet,
                   pr.employeecount AS EmployeeCount, pr.processedon AS ProcessedOn,
                   pr.isactive AS IsActive, pr.versionno AS VersionNo,
                   pr.createdby AS CreatedBy, pr.createdon AS CreatedOn,
                   pr.updatedby AS UpdatedBy, pr.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePayrollRuns} pr
            WHERE pr.payyear = @PayYear AND pr.paymonth = @PayMonth AND pr.isactive = true{branchFilter};
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayrollRunEntity>(new CommandDefinition(
                sql,
                new { PayYear = payYear, PayMonth = payMonth, ActiveBranchId = activeBranchId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<PayrollRunEntity?> GetRunByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, branchid AS BranchId, payyear AS PayYear, paymonth AS PayMonth, status AS Status,
                   useattendancewisesalary AS UseAttendanceWiseSalary,
                   totalgross AS TotalGross, totaldeductions AS TotalDeductions, totalnet AS TotalNet,
                   employeecount AS EmployeeCount, processedon AS ProcessedOn,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePayrollRuns}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayrollRunEntity>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateRunAsync(PayrollRunEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePayrollRuns}
                (id, branchid, payyear, paymonth, status, useattendancewisesalary,
                 totalgross, totaldeductions, totalnet, employeecount, processedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @PayYear, @PayMonth, @Status, @UseAttendanceWiseSalary,
                 @TotalGross, @TotalDeductions, @TotalNet, @EmployeeCount, @ProcessedOn,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateRunAsync(PayrollRunEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), SchoolLocalTime.NowDateTime());
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TablePayrollRuns}
            SET status = @Status,
                useattendancewisesalary = @UseAttendanceWiseSalary,
                totalgross = @TotalGross,
                totaldeductions = @TotalDeductions,
                totalnet = @TotalNet,
                employeecount = @EmployeeCount,
                processedon = @ProcessedOn,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task DeleteEntriesForRunAsync(Guid runId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                DELETE FROM {Schema}.{DatabaseConfig.TablePayrollEntryLines}
                WHERE payrollentryid IN (
                    SELECT id FROM {Schema}.{DatabaseConfig.TablePayrollEntries}
                    WHERE payrollrunid = @RunId);
                """,
                new { RunId = runId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false);

            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                DELETE FROM {Schema}.{DatabaseConfig.TablePayrollEntries}
                WHERE payrollrunid = @RunId;
                """,
                new { RunId = runId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IList<PayrollEntryListRow>> GetEntriesForRunAsync(Guid runId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT pe.id AS Id,
                   pe.employeeid AS EmployeeRecordId,
                   TRIM(u.firstname || ' ' || u.lastname) AS EmployeeName,
                   {DepartmentExpr} AS Department,
                   pe.basicsalary AS BasicSalary,
                   COALESCE((
                       SELECT SUM(l.amount)
                       FROM {Schema}.{DatabaseConfig.TablePayrollEntryLines} l
                       WHERE l.payrollentryid = pe.id AND l.isactive = true AND l.isearning = true
                         AND l.componentname ILIKE 'HRA%'
                   ), 0) AS HraAmount,
                   COALESCE((
                       SELECT SUM(l.amount)
                       FROM {Schema}.{DatabaseConfig.TablePayrollEntryLines} l
                       WHERE l.payrollentryid = pe.id AND l.isactive = true AND l.isearning = true
                         AND l.componentname NOT ILIKE 'Basic%'
                         AND l.componentname NOT ILIKE 'HRA%'
                   ), 0) AS Allowances,
                   pe.grosssalary AS GrossSalary,
                   pe.totaldeductions AS TotalDeductions,
                   pe.netsalary AS NetSalary,
                   pe.workingdays AS WorkingDays,
                   pe.presentdays AS PresentDays,
                   COALESCE((
                       SELECT SUM(l.amount)
                       FROM {Schema}.{DatabaseConfig.TablePayrollEntryLines} l
                       WHERE l.payrollentryid = pe.id AND l.isactive = true AND l.isearning = false
                         AND l.componentname = 'Attendance cut'
                   ), 0) AS AttendanceCutAmount,
                   pe.status AS Status
            FROM {Schema}.{DatabaseConfig.TablePayrollEntries} pe
            INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = pe.employeeid
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = t.userid
            WHERE pe.payrollrunid = @RunId AND pe.isactive = true
            ORDER BY EmployeeName;
            """;
        IEnumerable<PayrollEntryListRow> rows = await connection
            .QueryAsync<PayrollEntryListRow>(new CommandDefinition(sql, new { RunId = runId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<PayrollEntryEntity?> GetEntryByIdAsync(Guid entryId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, payrollrunid AS PayrollRunId, employeeid AS EmployeeId,
                   basicsalary AS BasicSalary,
                   grosssalary AS GrossSalary, totaldeductions AS TotalDeductions,
                   netsalary AS NetSalary, status AS Status,
                   workingdays AS WorkingDays, presentdays AS PresentDays,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePayrollEntries}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayrollEntryEntity>(new CommandDefinition(sql, new { Id = entryId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateEntryAsync(PayrollEntryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePayrollEntries}
                (id, payrollrunid, employeeid, basicsalary, grosssalary,
                 totaldeductions, netsalary, status, workingdays, presentdays,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @PayrollRunId, @EmployeeId, @BasicSalary, @GrossSalary,
                 @TotalDeductions, @NetSalary, @Status, @WorkingDays, @PresentDays,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task CreateEntryLinesAsync(IEnumerable<PayrollEntryLineEntity> lines, CancellationToken ct = default)
    {
        IList<PayrollEntryLineEntity> lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        foreach (PayrollEntryLineEntity line in lineList)
        {
            line.Id = line.Id == Guid.Empty ? Guid.NewGuid() : line.Id;
            EnsureInsertAudit(line, utcNow, actorId);
        }

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePayrollEntryLines}
                (id, payrollentryid, salaryversioncomponentid, componentname, componenttype, amount, isearning,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @PayrollEntryId, @SalaryVersionComponentId, @ComponentName, @ComponentType, @Amount, @IsEarning,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, lineList, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateEntryStatusAsync(Guid entryId, PayrollEntryStatus status, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TablePayrollEntries}
            SET status = @Status, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = entryId, Status = (short)status, UpdatedBy = actorId, UpdatedOn = utcNow },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkEntriesPaidAsync(Guid runId, IEnumerable<Guid>? entryIds, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        IList<Guid> ids = entryIds?.ToList() ?? [];

        string sql = ids.Count > 0
            ? $"""
            UPDATE {Schema}.{DatabaseConfig.TablePayrollEntries}
            SET status = @PaidStatus, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE payrollrunid = @RunId AND id = ANY(@EntryIds) AND isactive = true;
            """
            : $"""
            UPDATE {Schema}.{DatabaseConfig.TablePayrollEntries}
            SET status = @PaidStatus, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE payrollrunid = @RunId AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                RunId = runId,
                EntryIds = ids.ToArray(),
                PaidStatus = (short)PayrollEntryStatus.Paid,
                UpdatedBy = actorId,
                UpdatedOn = utcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<PayrollEntryLineEntity>> GetLinesForEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, payrollentryid AS PayrollEntryId, salaryversioncomponentid AS SalaryVersionComponentId,
                   componentname AS ComponentName, componenttype AS ComponentType, amount AS Amount,
                   isearning AS IsEarning, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePayrollEntryLines}
            WHERE payrollentryid = @EntryId AND isactive = true
            ORDER BY isearning DESC, componentname;
            """;
        IEnumerable<PayrollEntryLineEntity> rows = await connection
            .QueryAsync<PayrollEntryLineEntity>(new CommandDefinition(sql, new { EntryId = entryId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<PayslipContextRow?> GetPayslipContextAsync(Guid entryId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT pe.id AS EntryId,
                   pe.payrollrunid AS RunId,
                   pr.payyear AS PayYear,
                   pr.paymonth AS PayMonth,
                   pe.employeeid AS EmployeeRecordId,
                   TRIM(u.firstname || ' ' || u.lastname) AS EmployeeName,
                   t.employeecode AS EmployeeCode,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   pe.basicsalary AS BasicSalary,
                   pe.grosssalary AS GrossSalary,
                   pe.totaldeductions AS TotalDeductions,
                   pe.netsalary AS NetSalary,
                   pe.workingdays AS WorkingDays,
                   pe.presentdays AS PresentDays,
                   pr.useattendancewisesalary AS UseAttendanceWiseSalary,
                   t.bankname AS BankName,
                   t.bankaccountnumber AS BankAccountNumber,
                   t.bankifsccode AS BankIfscCode
            FROM {Schema}.{DatabaseConfig.TablePayrollEntries} pe
            INNER JOIN {Schema}.{DatabaseConfig.TablePayrollRuns} pr ON pr.id = pe.payrollrunid
            INNER JOIN {Schema}.{DatabaseConfig.TableEmployees} t ON t.id = pe.employeeid
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = t.userid
            WHERE pe.id = @EntryId AND pe.isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayslipContextRow>(new CommandDefinition(sql, new { EntryId = entryId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
