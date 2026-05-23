using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Configuration;
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
        Guid? departmentId,
        string? designation,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS TeacherId,
                   TRIM(t.firstname || ' ' || t.lastname) AS EmployeeName,
                   t.employeeid AS EmployeeId,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   es.id AS EmployeeSalaryId,
                   es.salarystructureversionid AS SalaryStructureVersionId
            FROM {Schema}.{DatabaseConfig.TableTeachers} t
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployeeSalaries} es
                ON es.teacherid = t.id AND es.isactive = true
            WHERE t.isactive = true
            {(string.IsNullOrWhiteSpace(search) ? string.Empty : "AND (TRIM(t.firstname || ' ' || t.lastname) ILIKE @Search OR COALESCE(t.employeeid, '') ILIKE @Search)")}
            {(departmentId.HasValue ? "AND t.departmentid = @DepartmentId" : string.Empty)}
            {(string.IsNullOrWhiteSpace(designation) ? string.Empty : "AND t.designation ILIKE @Designation")}
            ORDER BY EmployeeName;
            """;

        IEnumerable<EmployeeSalaryListRow> rows = await connection.QueryAsync<EmployeeSalaryListRow>(new CommandDefinition(
            sql,
            new
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
                DepartmentId = departmentId,
                Designation = string.IsNullOrWhiteSpace(designation) ? null : $"%{designation.Trim()}%"
            },
            cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<EmployeeSalaryEntity?> GetActiveAssignmentByTeacherIdAsync(Guid teacherId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, teacherid AS TeacherId, salarystructureversionid AS SalaryStructureVersionId,
                   effectivedate AS EffectiveDate,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            WHERE teacherid = @TeacherId AND isactive = true
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<EmployeeSalaryEntity>(new CommandDefinition(sql, new { TeacherId = teacherId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<TeacherSalaryContextRow?> GetTeacherSalaryContextAsync(Guid teacherId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT t.id AS TeacherId,
                   TRIM(t.firstname || ' ' || t.lastname) AS EmployeeName,
                   t.employeeid AS EmployeeId,
                   {DepartmentExpr} AS Department,
                   t.designation AS Designation,
                   t.bankname AS BankName,
                   t.bankaccountnumber AS BankAccountNumber,
                   t.bankifsccode AS BankIfscCode,
                   es.id AS EmployeeSalaryId,
                   es.salarystructureversionid AS SalaryStructureVersionId,
                   es.effectivedate AS EffectiveDate
            FROM {Schema}.{DatabaseConfig.TableTeachers} t
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployeeSalaries} es
                ON es.teacherid = t.id AND es.isactive = true
            WHERE t.id = @TeacherId AND t.isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<TeacherSalaryContextRow>(new CommandDefinition(sql, new { TeacherId = teacherId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task DeactivateAssignmentsForTeacherAsync(Guid teacherId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE teacherid = @TeacherId AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TeacherId = teacherId, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableEmployeeSalaries}
                (id, teacherid, salarystructureversionid, effectivedate,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @TeacherId, @SalaryStructureVersionId, @EffectiveDate,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAssignmentAsync(EmployeeSalaryEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableEmployeeSalaries}
            SET salarystructureversionid = @SalaryStructureVersionId,
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
            DateTime utcNow = DateTime.UtcNow;

            // Physical delete avoids unique constraint on (employeesalaryid, salaryversioncomponentid)
            // when prior soft-deleted rows still exist.
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
            SELECT id AS Id, teacherid AS TeacherId, salarystructureversionid AS SalaryStructureVersionId,
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
