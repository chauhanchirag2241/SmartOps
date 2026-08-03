using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Salary;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Salary;

public sealed class EmployeeSalaryRepository : BaseRepository, IEmployeeSalaryRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public EmployeeSalaryRepository(
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

    public async Task<IList<EmployeeSalaryListRow>> GetEmployeeSalariesAsync(
        string? search,
        IReadOnlyList<Guid>? userTypeIds,
        CancellationToken ct = default)
    {
        if (userTypeIds is null || userTypeIds.Count == 0)
        {
            return [];
        }

        Guid[] typeIds = userTypeIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (typeIds.Length == 0)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS EmployeeRecordId,
                   TRIM(u.firstname || ' ' || u.lastname) AS EmployeeName,
                   t.employeecode AS EmployeeCode,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   u.usertypeid AS UserTypeId,
                   es.id AS EmployeeSalaryId,
                   es.salarystructureid AS SalaryStructureVersionId
            FROM {Schema}.{DatabaseConfig.TableEmployees} t
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = t.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployeeSalaries} es
                ON es.employeeid = t.id AND es.isactive = true
            WHERE t.isactive = true
              AND u.usertypeid = ANY(@UserTypeIds)
            {(string.IsNullOrWhiteSpace(search) ? string.Empty : "AND (TRIM(u.firstname || ' ' || u.lastname) ILIKE @Search OR COALESCE(t.employeecode, '') ILIKE @Search)")}
            ORDER BY EmployeeName;
            """;

        IEnumerable<EmployeeSalaryListRow> rows = await connection.QueryAsync<EmployeeSalaryListRow>(new CommandDefinition(
            sql,
            new
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                UserTypeIds = typeIds
            },
            cancellationToken: ct)).ConfigureAwait(false);

        IList<EmployeeSalaryListRow> list = rows.ToList();
        foreach (EmployeeSalaryListRow row in list)
        {
            row.UserTypeName = UserTypeCodes.GetName(row.UserTypeId);
        }

        return list;
    }

    public async Task<EmployeeSalaryEntity?> GetActiveAssignmentByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, employeeid AS EmployeeId, salarystructureid AS SalaryStructureVersionId,
                   effectivedate AS EffectiveDate,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            WHERE employeeid = @EmployeeId AND isactive = true
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<EmployeeSalaryEntity>(new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<EmployeeSalaryContextRow?> GetEmployeeSalaryContextAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS EmployeeRecordId,
                   TRIM(u.firstname || ' ' || u.lastname) AS EmployeeName,
                   t.employeecode AS EmployeeCode,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   t.bankname AS BankName,
                   t.bankaccountnumber AS BankAccountNumber,
                   t.bankifsccode AS BankIfscCode,
                   es.id AS EmployeeSalaryId,
                   es.salarystructureid AS SalaryStructureVersionId,
                   es.effectivedate AS EffectiveDate
            FROM {Schema}.{DatabaseConfig.TableEmployees} t
            INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} u ON u.id = t.userid
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployeeSalaries} es
                ON es.employeeid = t.id AND es.isactive = true
            WHERE t.id = @EmployeeId AND t.isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<EmployeeSalaryContextRow>(new CommandDefinition(sql, new { EmployeeId = employeeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task DeactivateAssignmentsForEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE employeeid = @EmployeeId AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EmployeeId = employeeId, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableEmployeeSalaries}
                (id, employeeid, salarystructureid, effectivedate,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @EmployeeId, @SalaryStructureVersionId, @EffectiveDate,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), SchoolLocalTime.NowDateTime());
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            SET salarystructureid = @SalaryStructureVersionId,
                effectivedate = @EffectiveDate,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IList<EmployeeSalaryComponentEntity>> GetComponentValuesForAssignmentAsync(
        Guid employeeSalaryId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, employeesalaryid AS EmployeeSalaryId, salaryversioncomponentid AS SalaryVersionComponentId,
                   value AS Value, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableEmployeeSalaryComponents}
            WHERE employeesalaryid = @EmployeeSalaryId AND isactive = true;
            """;
        IEnumerable<EmployeeSalaryComponentEntity> rows = await connection
            .QueryAsync<EmployeeSalaryComponentEntity>(
                new CommandDefinition(sql, new { EmployeeSalaryId = employeeSalaryId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task ReplaceComponentValuesAsync(
        Guid employeeSalaryId,
        IEnumerable<EmployeeSalaryComponentEntity> values,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            Guid actorId = ResolveInsertActor();
            DateTime utcNow = SchoolLocalTime.NowDateTime();

            await conn.ExecuteAsync(
                $"""
                DELETE FROM {Schema}.{DatabaseConfig.TableEmployeeSalaryComponents}
                WHERE employeesalaryid = @EmployeeSalaryId;
                """,
                new { EmployeeSalaryId = employeeSalaryId },
                tx).ConfigureAwait(false);

            foreach (EmployeeSalaryComponentEntity row in values)
            {
                row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
                row.EmployeeSalaryId = employeeSalaryId;
                EnsureInsertAudit(row, utcNow, actorId);

                await conn.ExecuteAsync(
                    $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableEmployeeSalaryComponents}
                        (id, employeesalaryid, salaryversioncomponentid, value,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @EmployeeSalaryId, @SalaryVersionComponentId, @Value,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    row,
                    tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task<IList<EmployeeSalaryEntity>> GetActiveAssignmentsAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, employeeid AS EmployeeId, salarystructureid AS SalaryStructureVersionId,
                   effectivedate AS EffectiveDate,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            WHERE isactive = true;
            """;
        IEnumerable<EmployeeSalaryEntity> rows = await connection
            .QueryAsync<EmployeeSalaryEntity>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }
}
