using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Salary.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Salary;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Salary;

public sealed class SalaryStructureRepository : BaseRepository, ISalaryStructureRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public SalaryStructureRepository(
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

    public async Task<IList<SalaryStructureVersionListRow>> GetVersionsAsync(
        Guid? academicYearId,
        SalaryStructureVersionStatus? status,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "v", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT v.id AS Id,
                   v.academicyearid AS AcademicYearId,
                   COALESCE(ay.title, '') AS AcademicYearTitle,
                   v.versionnumber AS VersionNumber,
                   v.status AS Status,
                   v.effectivedate AS EffectiveDate,
                   v.publishedon AS PublishedOn,
                   v.activatedon AS ActivatedOn,
                   (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableSalaryVersionComponents} sc
                    WHERE sc.salarystructureversionid = v.id AND sc.isactive = true) AS ComponentCount,
                   EXISTS (
                       SELECT 1 FROM {Schema}.{DatabaseConfig.TableEmployeeSalaries} es
                       WHERE es.salarystructureversionid = v.id AND es.isactive = true
                   ) AS HasAssignedEmployees
            FROM {Schema}.{DatabaseConfig.TableSalaryStructureVersions} v
            LEFT JOIN {Schema}.{DatabaseConfig.TableAcademicYears} ay ON ay.id = v.academicyearid
            WHERE v.isactive = true{branchFilter}
            {(academicYearId.HasValue ? "AND v.academicyearid = @AcademicYearId" : string.Empty)}
            {(status.HasValue ? "AND v.status = @Status" : string.Empty)}
            ORDER BY ay.title, v.versionnumber DESC;
            """;

        IEnumerable<SalaryStructureVersionListRow> rows = await connection
            .QueryAsync<SalaryStructureVersionListRow>(new CommandDefinition(
                sql,
                new
                {
                    AcademicYearId = academicYearId,
                    Status = status.HasValue ? (short)status.Value : (short?)null,
                    ActiveBranchId = activeBranchId
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<SalaryStructureVersionEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, academicyearid AS AcademicYearId, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<SalaryStructureVersionEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<SalaryStructureVersionEntity?> GetActiveVersionForYearAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, academicyearid AS AcademicYearId, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            WHERE academicyearid = @AcademicYearId AND status = @ActiveStatus AND isactive = true
            ORDER BY versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<SalaryStructureVersionEntity>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, ActiveStatus = (short)SalaryStructureVersionStatus.Active },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<SalaryStructureVersionEntity?> GetAdmissionVersionForYearAsync(
        Guid academicYearId,
        CancellationToken ct = default)
    {
        SalaryStructureVersionEntity? active = await GetActiveVersionForYearAsync(academicYearId, ct).ConfigureAwait(false);
        if (active is not null)
        {
            return active;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, academicyearid AS AcademicYearId, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            WHERE academicyearid = @AcademicYearId
              AND status = @PublishedStatus
              AND isactive = true
            ORDER BY versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<SalaryStructureVersionEntity>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, PublishedStatus = (short)SalaryStructureVersionStatus.Published },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> GetNextVersionNumberAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(MAX(versionnumber), 0) + 1
            FROM {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            WHERE academicyearid = @AcademicYearId;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateVersionAsync(SalaryStructureVersionEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
                (id, branchid, academicyearid, versionnumber, status, effectivedate, publishedon, activatedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @AcademicYearId, @VersionNumber, @Status, @EffectiveDate, @PublishedOn, @ActivatedOn,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVersionAsync(SalaryStructureVersionEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            SET status = @Status,
                effectivedate = @EffectiveDate,
                publishedon = @PublishedOn,
                activatedon = @ActivatedOn,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteVersionAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task ArchiveActiveVersionsForYearAsync(Guid academicYearId, Guid exceptVersionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            SET status = @ArchivedStatus,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE academicyearid = @AcademicYearId
              AND id <> @ExceptVersionId
              AND status = @ActiveStatus
              AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                AcademicYearId = academicYearId,
                ExceptVersionId = exceptVersionId,
                ActiveStatus = (short)SalaryStructureVersionStatus.Active,
                ArchivedStatus = (short)SalaryStructureVersionStatus.Archived,
                UpdatedBy = actorId,
                UpdatedOn = utcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task ArchivePublishedVersionsForYearAsync(
        Guid academicYearId,
        Guid exceptVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryStructureVersions}
            SET status = @ArchivedStatus,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE academicyearid = @AcademicYearId
              AND id <> @ExceptVersionId
              AND status = @PublishedStatus
              AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                AcademicYearId = academicYearId,
                ExceptVersionId = exceptVersionId,
                PublishedStatus = (short)SalaryStructureVersionStatus.Published,
                ArchivedStatus = (short)SalaryStructureVersionStatus.Archived,
                UpdatedBy = actorId,
                UpdatedOn = utcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<bool> VersionHasAssignedEmployeesAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableEmployeeSalaries}
                WHERE salarystructureversionid = @VersionId AND isactive = true);
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CloneVersionAsync(Guid sourceVersionId, Guid newVersionId, CancellationToken ct = default)
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

            IList<SalaryVersionComponentEntity> sourceComponents = (await connection.QueryAsync<SalaryVersionComponentEntity>(new CommandDefinition(
                $"""
                SELECT id AS Id, salarystructureversionid AS SalaryStructureVersionId, name AS Name,
                       shortcode AS ShortCode, componenttype AS ComponentType,
                       calculationtype AS CalculationType, value AS Value, istaxable AS IsTaxable,
                       isactive AS IsActive
                FROM {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
                WHERE salarystructureversionid = @SourceVersionId AND isactive = true;
                """,
                new { SourceVersionId = sourceVersionId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false)).ToList();

            foreach (SalaryVersionComponentEntity sourceComponent in sourceComponents)
            {
                var cloneComponent = new SalaryVersionComponentEntity
                {
                    Id = Guid.NewGuid(),
                    SalaryStructureVersionId = newVersionId,
                    Name = sourceComponent.Name,
                    ShortCode = sourceComponent.ShortCode,
                    ComponentType = sourceComponent.ComponentType,
                    CalculationType = sourceComponent.CalculationType,
                    Value = sourceComponent.Value,
                    IsTaxable = sourceComponent.IsTaxable
                };
                EnsureInsertAudit(cloneComponent, utcNow, actorId);
                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
                        (id, salarystructureversionid, name, shortcode, componenttype, calculationtype, value, istaxable,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @SalaryStructureVersionId, @Name, @ShortCode, @ComponentType, @CalculationType, @Value, @IsTaxable,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    cloneComponent,
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);
            }

            transaction.Commit();
            return newVersionId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IList<SalaryVersionComponentListRow>> GetComponentsAsync(
        Guid salaryStructureVersionId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT sc.id AS Id,
                   sc.salarystructureversionid AS SalaryStructureVersionId,
                   sc.name AS Name,
                   sc.shortcode AS ShortCode,
                   sc.componenttype AS ComponentType,
                   sc.calculationtype AS CalculationType,
                   sc.value AS Value,
                   sc.istaxable AS IsTaxable,
                   sc.isactive AS IsActive
            FROM {Schema}.{DatabaseConfig.TableSalaryVersionComponents} sc
            WHERE sc.salarystructureversionid = @VersionId AND sc.isactive = true
            ORDER BY sc.componenttype, sc.name;
            """;
        IEnumerable<SalaryVersionComponentListRow> rows = await connection
            .QueryAsync<SalaryVersionComponentListRow>(new CommandDefinition(
                sql,
                new { VersionId = salaryStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<SalaryVersionComponentEntity?> GetComponentByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, salarystructureversionid AS SalaryStructureVersionId, name AS Name,
                   shortcode AS ShortCode, componenttype AS ComponentType,
                   calculationtype AS CalculationType, value AS Value, istaxable AS IsTaxable,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
            WHERE id = @Id;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<SalaryVersionComponentEntity>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateComponentAsync(SalaryVersionComponentEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
                (id, salarystructureversionid, name, shortcode, componenttype, calculationtype, value, istaxable,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @SalaryStructureVersionId, @Name, @ShortCode, @ComponentType, @CalculationType, @Value, @IsTaxable,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateComponentAsync(SalaryVersionComponentEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
            SET name = @Name,
                shortcode = @ShortCode,
                componenttype = @ComponentType,
                calculationtype = @CalculationType,
                value = @Value,
                istaxable = @IsTaxable,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteComponentAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveComponentsForVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableSalaryVersionComponents}
            WHERE salarystructureversionid = @VersionId AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<string?> GetAcademicYearTitleAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT title FROM {Schema}.{DatabaseConfig.TableAcademicYears}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { Id = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
