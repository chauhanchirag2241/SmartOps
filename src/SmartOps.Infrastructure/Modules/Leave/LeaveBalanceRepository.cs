using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Leave;

public sealed class LeaveBalanceRepository : BaseRepository, ILeaveBalanceRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public LeaveBalanceRepository(
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

    private string G => IdentitySchema;

    public async Task<IList<LeaveBalanceListRow>> GetByEmployeeAsync(
        Guid employeeId, Guid? academicYearId = null, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid? yearId = academicYearId ?? await GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT b.id AS Id, b.employeeid AS EmployeeId,
                   u.firstname AS EmployeeFirstName, u.lastname AS EmployeeLastName,
                   b.leavetypeid AS LeaveTypeId, lt.name AS LeaveTypeName,
                   b.academicyearid AS AcademicYearId,
                   b.openingbalance AS OpeningBalance, b.accrued AS Accrued, b.used AS Used,
                   b.adjusted AS Adjusted, b.closingbalance AS ClosingBalance
            FROM {Schema}.{DatabaseConfig.TableLeaveBalances} b
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} e ON e.id = b.employeeid
            LEFT JOIN {G}.{DatabaseConfig.TableUsers} u ON u.id = e.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = b.leavetypeid
            WHERE b.isactive = true AND b.employeeid = @EmployeeId
              AND (@AcademicYearId IS NULL OR b.academicyearid = @AcademicYearId)
            ORDER BY lt.sortorder, lt.name;
            """;
        var rows = await connection.QueryAsync<LeaveBalanceListRow>(new CommandDefinition(
            sql, new { EmployeeId = employeeId, AcademicYearId = yearId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<LeaveBalanceEntity?> GetBalanceAsync(
        Guid employeeId, Guid leaveTypeId, Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        return await GetBalanceInternalAsync(connection, Schema, employeeId, leaveTypeId, academicYearId, null, ct)
            .ConfigureAwait(false);
    }

    public async Task UpsertBalanceAsync(LeaveBalanceEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await UpsertBalanceInternalAsync(connection, Schema, entity, null, ct).ConfigureAwait(false);
    }

    public async Task InsertLedgerAsync(LeaveLedgerEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await InsertLedgerInternalAsync(connection, Schema, entity, null, ct).ConfigureAwait(false);
    }

    public async Task<IList<LeaveLedgerListRow>> GetLedgerAsync(
        Guid employeeId, Guid? leaveTypeId = null, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT l.id AS Id, l.employeeid AS EmployeeId, l.leavetypeid AS LeaveTypeId,
                   lt.name AS LeaveTypeName, l.academicyearid AS AcademicYearId,
                   l.txntype AS TxnType, l.days AS Days, l.balanceafter AS BalanceAfter,
                   l.referenceid AS ReferenceId, l.remark AS Remark, l.txndate AS TxnDate,
                   l.createdon AS CreatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveLedger} l
            LEFT JOIN {Schema}.{DatabaseConfig.TableLeaveTypes} lt ON lt.id = l.leavetypeid
            WHERE l.employeeid = @EmployeeId
              AND (@LeaveTypeId IS NULL OR l.leavetypeid = @LeaveTypeId)
            ORDER BY l.createdon DESC, l.txndate DESC;
            """;
        var rows = await connection.QueryAsync<LeaveLedgerListRow>(new CommandDefinition(
            sql, new { EmployeeId = employeeId, LeaveTypeId = leaveTypeId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid?> GetCurrentAcademicYearIdAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        return await GetCurrentAcademicYearIdAsync(connection, Schema, ct).ConfigureAwait(false);
    }

    public async Task<LeaveTypeEntity?> GetLeaveTypeAsync(Guid leaveTypeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, code AS Code, name AS Name, ispaid AS IsPaid, requiresbalance AS RequiresBalance,
                   allowhalfday AS AllowHalfDay, carryforward AS CarryForward, sortorder AS SortOrder,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableLeaveTypes}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection.QuerySingleOrDefaultAsync<LeaveTypeEntity>(
            new CommandDefinition(sql, new { Id = leaveTypeId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid?> GetCurrentAcademicYearIdAsync(
        IDbConnection connection, string schema, CancellationToken ct = default)
    {
        string sql = $"""
            SELECT id FROM {schema}.{DatabaseConfig.TableAcademicYears}
            WHERE isactive = true
              AND CURRENT_DATE BETWEEN startdate AND enddate
            ORDER BY startdate DESC
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<Guid?> TryStartAccrualRunAsync(
        IDbConnection connection, string schema, int year, int month, CancellationToken ct = default)
    {
        Guid runId = Guid.NewGuid();
        string sql = $"""
            INSERT INTO {schema}.{DatabaseConfig.TableLeaveAccrualRuns}
                (id, year, month, ranon, status, employeesscored, errorlog)
            VALUES
                (@Id, @Year, @Month, @RanOn, @Status, 0, NULL)
            ON CONFLICT (year, month) DO NOTHING
            RETURNING id;
            """;
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            sql,
            new
            {
                Id = runId,
                Year = year,
                Month = month,
                RanOn = SchoolLocalTime.Now(),
                Status = (short)LeaveAccrualRunStatus.Running
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task MarkAccrualRunAsync(
        IDbConnection connection,
        string schema,
        Guid runId,
        LeaveAccrualRunStatus status,
        int employeesScored,
        string? errorLog,
        CancellationToken ct = default)
    {
        string sql = $"""
            UPDATE {schema}.{DatabaseConfig.TableLeaveAccrualRuns}
            SET status = @Status,
                employeesscored = @EmployeesScored,
                errorlog = @ErrorLog,
                ranon = @RanOn
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = runId,
                Status = (short)status,
                EmployeesScored = employeesScored,
                ErrorLog = errorLog,
                RanOn = SchoolLocalTime.Now()
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<EmployeeUserTypeRow>> ListActiveEmployeesWithUserTypeAsync(
        IDbConnection connection,
        string schoolSchema,
        string identitySchema,
        CancellationToken ct = default)
    {
        string sql = $"""
            SELECT e.id AS EmployeeId, u.usertypeid AS UserTypeId
            FROM {schoolSchema}.{DatabaseConfig.TableEmployees} e
            INNER JOIN {identitySchema}.{DatabaseConfig.TableUsers} u ON u.id = e.userid AND u.isactive = true
            WHERE e.isactive = true AND e.userid IS NOT NULL;
            """;
        var rows = await connection.QueryAsync<EmployeeUserTypeRow>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<LeavePolicyEntity>> GetActivePoliciesAsync(
        IDbConnection connection, string schema, CancellationToken ct = default)
    {
        string sql = $"""
            SELECT id AS Id, usertypeid AS UserTypeId, leavetypeid AS LeaveTypeId, monthlyleave AS MonthlyLeave,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {schema}.{DatabaseConfig.TableLeavePolicies}
            WHERE isactive = true AND monthlyleave > 0;
            """;
        var rows = await connection.QueryAsync<LeavePolicyEntity>(
            new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task ApplyAccrualCreditAsync(
        IDbConnection connection,
        string schema,
        Guid employeeId,
        Guid leaveTypeId,
        Guid academicYearId,
        decimal days,
        Guid actorId,
        DateOnly txnDate,
        string? remark,
        CancellationToken ct = default)
    {
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            LeaveBalanceEntity? balance = await GetBalanceInternalAsync(
                conn, schema, employeeId, leaveTypeId, academicYearId, tx, ct).ConfigureAwait(false);

            if (balance is null)
            {
                balance = new LeaveBalanceEntity
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveTypeId,
                    AcademicYearId = academicYearId,
                    OpeningBalance = 0,
                    Accrued = days,
                    Used = 0,
                    Adjusted = 0,
                    ClosingBalance = days,
                    CreatedBy = actorId,
                    UpdatedBy = actorId,
                    CreatedOn = SchoolLocalTime.NowDateTime(),
                    UpdatedOn = SchoolLocalTime.NowDateTime(),
                    IsActive = true,
                    VersionNo = 1
                };
            }
            else
            {
                balance.Accrued += days;
                balance.ClosingBalance = balance.OpeningBalance + balance.Accrued - balance.Used + balance.Adjusted;
                balance.UpdatedBy = actorId;
                balance.UpdatedOn = SchoolLocalTime.NowDateTime();
            }

            await UpsertBalanceInternalAsync(conn, schema, balance, tx, ct).ConfigureAwait(false);

            var ledger = new LeaveLedgerEntity
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                AcademicYearId = academicYearId,
                TxnType = LeaveLedgerTxnType.Accrual,
                Days = days,
                BalanceAfter = balance.ClosingBalance,
                Remark = remark,
                TxnDate = txnDate,
                CreatedBy = actorId,
                CreatedOn = SchoolLocalTime.Now()
            };
            await InsertLedgerInternalAsync(conn, schema, ledger, tx, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private static async Task<LeaveBalanceEntity?> GetBalanceInternalAsync(
        IDbConnection connection,
        string schema,
        Guid employeeId,
        Guid leaveTypeId,
        Guid academicYearId,
        IDbTransaction? tx,
        CancellationToken ct)
    {
        string sql = $"""
            SELECT id AS Id, employeeid AS EmployeeId, leavetypeid AS LeaveTypeId,
                   academicyearid AS AcademicYearId, openingbalance AS OpeningBalance,
                   accrued AS Accrued, used AS Used, adjusted AS Adjusted, closingbalance AS ClosingBalance,
                   isactive AS IsActive, versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {schema}.{DatabaseConfig.TableLeaveBalances}
            WHERE employeeid = @EmployeeId AND leavetypeid = @LeaveTypeId
              AND academicyearid = @AcademicYearId AND isactive = true
            LIMIT 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<LeaveBalanceEntity>(
            new CommandDefinition(
                sql,
                new { EmployeeId = employeeId, LeaveTypeId = leaveTypeId, AcademicYearId = academicYearId },
                transaction: tx,
                cancellationToken: ct)).ConfigureAwait(false);
    }

    private async Task UpsertBalanceInternalAsync(
        IDbConnection connection,
        string schema,
        LeaveBalanceEntity entity,
        IDbTransaction? tx,
        CancellationToken ct)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        if (entity.CreatedOn == default)
        {
            EnsureInsertAudit(entity, SchoolLocalTime.NowDateTime());
        }

        string sql = $"""
            INSERT INTO {schema}.{DatabaseConfig.TableLeaveBalances}
                (id, employeeid, leavetypeid, academicyearid, openingbalance, accrued, used, adjusted, closingbalance,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @EmployeeId, @LeaveTypeId, @AcademicYearId, @OpeningBalance, @Accrued, @Used, @Adjusted, @ClosingBalance,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
            ON CONFLICT (employeeid, leavetypeid, academicyearid) DO UPDATE SET
                openingbalance = EXCLUDED.openingbalance,
                accrued = EXCLUDED.accrued,
                used = EXCLUDED.used,
                adjusted = EXCLUDED.adjusted,
                closingbalance = EXCLUDED.closingbalance,
                updatedby = EXCLUDED.updatedby,
                updatedon = EXCLUDED.updatedon,
                versionno = {schema}.{DatabaseConfig.TableLeaveBalances}.versionno + 1,
                isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, transaction: tx, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private static async Task InsertLedgerInternalAsync(
        IDbConnection connection,
        string schema,
        LeaveLedgerEntity entity,
        IDbTransaction? tx,
        CancellationToken ct)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        string sql = $"""
            INSERT INTO {schema}.{DatabaseConfig.TableLeaveLedger}
                (id, employeeid, leavetypeid, academicyearid, txntype, days, balanceafter,
                 referenceid, remark, txndate, createdby, createdon)
            VALUES
                (@Id, @EmployeeId, @LeaveTypeId, @AcademicYearId, @TxnType, @Days, @BalanceAfter,
                 @ReferenceId, @Remark, @TxnDate, @CreatedBy, @CreatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                entity.Id,
                entity.EmployeeId,
                entity.LeaveTypeId,
                entity.AcademicYearId,
                TxnType = (short)entity.TxnType,
                entity.Days,
                entity.BalanceAfter,
                entity.ReferenceId,
                entity.Remark,
                entity.TxnDate,
                entity.CreatedBy,
                entity.CreatedOn
            },
            transaction: tx,
            cancellationToken: ct)).ConfigureAwait(false);
    }
}
