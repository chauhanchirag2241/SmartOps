using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Salary;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Salary;

public sealed class PayrollRepository : BaseRepository, IPayrollRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public PayrollRepository(
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

    private string DepartmentExpr => SalaryDepartmentSql.DepartmentSubquery(Schema, "t");

    public async Task<PayrollRunEntity?> GetRunByYearMonthAsync(int payYear, int payMonth, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, payyear AS PayYear, paymonth AS PayMonth, status AS Status,
                   useattendancewisesalary AS UseAttendanceWiseSalary,
                   totalgross AS TotalGross, totaldeductions AS TotalDeductions, totalnet AS TotalNet,
                   employeecount AS EmployeeCount, processedon AS ProcessedOn,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TablePayrollRuns}
            WHERE payyear = @PayYear AND paymonth = @PayMonth AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayrollRunEntity>(new CommandDefinition(sql, new { PayYear = payYear, PayMonth = payMonth }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<PayrollRunEntity?> GetRunByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, payyear AS PayYear, paymonth AS PayMonth, status AS Status,
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
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePayrollRuns}
                (id, payyear, paymonth, status, useattendancewisesalary,
                 totalgross, totaldeductions, totalnet, employeecount, processedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @PayYear, @PayMonth, @Status, @UseAttendanceWiseSalary,
                 @TotalGross, @TotalDeductions, @TotalNet, @EmployeeCount, @ProcessedOn,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateRunAsync(PayrollRunEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
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
                   pe.teacherid AS TeacherId,
                   TRIM(t.firstname || ' ' || t.lastname) AS EmployeeName,
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
                   pe.status AS Status
            FROM {Schema}.{DatabaseConfig.TablePayrollEntries} pe
            INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = pe.teacherid
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
            SELECT id AS Id, payrollrunid AS PayrollRunId, teacherid AS TeacherId,
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
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TablePayrollEntries}
                (id, payrollrunid, teacherid, basicsalary, grosssalary,
                 totaldeductions, netsalary, status, workingdays, presentdays,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @PayrollRunId, @TeacherId, @BasicSalary, @GrossSalary,
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
        DateTime utcNow = DateTime.UtcNow;
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
        DateTime utcNow = DateTime.UtcNow;
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
        DateTime utcNow = DateTime.UtcNow;
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
                   pe.teacherid AS TeacherId,
                   TRIM(t.firstname || ' ' || t.lastname) AS EmployeeName,
                   t.employeeid AS EmployeeId,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   pe.basicsalary AS BasicSalary,
                   pe.grosssalary AS GrossSalary,
                   pe.totaldeductions AS TotalDeductions,
                   pe.netsalary AS NetSalary,
                   pe.workingdays AS WorkingDays,
                   pe.presentdays AS PresentDays,
                   t.bankname AS BankName,
                   t.bankaccountnumber AS BankAccountNumber,
                   t.bankifsccode AS BankIfscCode
            FROM {Schema}.{DatabaseConfig.TablePayrollEntries} pe
            INNER JOIN {Schema}.{DatabaseConfig.TablePayrollRuns} pr ON pr.id = pe.payrollrunid
            INNER JOIN {Schema}.{DatabaseConfig.TableTeachers} t ON t.id = pe.teacherid
            WHERE pe.id = @EntryId AND pe.isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<PayslipContextRow>(new CommandDefinition(sql, new { EntryId = entryId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
