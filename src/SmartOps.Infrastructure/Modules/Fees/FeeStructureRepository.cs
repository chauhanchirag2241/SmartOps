using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class FeeStructureRepository : BaseRepository, IFeeStructureRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public FeeStructureRepository(
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

    public async Task<IList<FeeStructureVersionListRow>> GetVersionsAsync(
        Guid? academicYearId,
        FeeStructureVersionStatus? status,
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
                   (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
                    WHERE ft.feestructureversionid = v.id AND ft.isactive = true) AS FeeTypeCount,
                   EXISTS (
                       SELECT 1 FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                       WHERE fp.feestructureversionid = v.id AND fp.isactive = true
                   ) AS HasStudentPayments
            FROM {Schema}.{DatabaseConfig.TableFeeStructureVersions} v
            LEFT JOIN {Schema}.{DatabaseConfig.TableAcademicYears} ay ON ay.id = v.academicyearid
            WHERE v.isactive = true{branchFilter}
            {(academicYearId.HasValue ? "AND v.academicyearid = @AcademicYearId" : string.Empty)}
            {(status.HasValue ? "AND v.status = @Status" : string.Empty)}
            ORDER BY ay.title, v.versionnumber DESC;
            """;

        IEnumerable<FeeStructureVersionListRow> rows = await connection
            .QueryAsync<FeeStructureVersionListRow>(new CommandDefinition(
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

    public async Task<FeeStructureVersionEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, academicyearid AS AcademicYearId, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeStructureVersions}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureVersionEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeStructureVersionEntity?> GetActiveVersionForYearAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, academicyearid AS AcademicYearId, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeStructureVersions}
            WHERE academicyearid = @AcademicYearId AND status = @ActiveStatus AND isactive = true
            ORDER BY versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureVersionEntity>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, ActiveStatus = (short)FeeStructureVersionStatus.Active },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeStructureVersionEntity?> GetAdmissionVersionForYearAsync(
        Guid academicYearId,
        CancellationToken ct = default)
    {
        FeeStructureVersionEntity? active = await GetActiveVersionForYearAsync(academicYearId, ct).ConfigureAwait(false);
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
            FROM {Schema}.{DatabaseConfig.TableFeeStructureVersions}
            WHERE academicyearid = @AcademicYearId
              AND status = @PublishedStatus
              AND isactive = true
            ORDER BY versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureVersionEntity>(new CommandDefinition(
                sql,
                new { AcademicYearId = academicYearId, PublishedStatus = (short)FeeStructureVersionStatus.Published },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> GetNextVersionNumberAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(MAX(versionnumber), 0) + 1
            FROM {Schema}.{DatabaseConfig.TableFeeStructureVersions}
            WHERE academicyearid = @AcademicYearId;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateVersionAsync(FeeStructureVersionEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableFeeStructureVersions}
                (id, branchid, academicyearid, versionnumber, status, effectivedate, publishedon, activatedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @AcademicYearId, @VersionNumber, @Status, @EffectiveDate, @PublishedOn, @ActivatedOn,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVersionAsync(FeeStructureVersionEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructureVersions}
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
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructureVersions}
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
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructureVersions}
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
                ActiveStatus = (short)FeeStructureVersionStatus.Active,
                ArchivedStatus = (short)FeeStructureVersionStatus.Archived,
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
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructureVersions}
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
                PublishedStatus = (short)FeeStructureVersionStatus.Published,
                ArchivedStatus = (short)FeeStructureVersionStatus.Archived,
                UpdatedBy = actorId,
                UpdatedOn = utcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<bool> VersionHasPaymentsAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableFeePayments}
                WHERE feestructureversionid = @VersionId AND isactive = true);
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> VersionHasAssignedStudentsAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableStudentAcademics}
                WHERE feestructureversionid = @VersionId AND isactive = true);
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

            IList<FeeTypeEntity> sourceTypes = (await connection.QueryAsync<FeeTypeEntity>(new CommandDefinition(
                $"""
                SELECT id AS Id, feestructureversionid AS FeeStructureVersionId, name AS Name,
                       category AS Category, frequency AS CollectionType,
                       ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                       COALESCE(studentwisedifferentamount, false) AS StudentWiseDifferentAmount,
                       isactive AS IsActive
                FROM {Schema}.{DatabaseConfig.TableFeeTypes}
                WHERE feestructureversionid = @SourceVersionId AND isactive = true;
                """,
                new { SourceVersionId = sourceVersionId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false)).ToList();

            var typeMap = new Dictionary<Guid, Guid>();
            foreach (FeeTypeEntity sourceType in sourceTypes)
            {
                var cloneType = new FeeTypeEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureVersionId = newVersionId,
                    Name = sourceType.Name,
                    Category = sourceType.Category,
                    CollectionType = sourceType.CollectionType,
                    IsMandatory = sourceType.IsMandatory,
                    IsRefundable = sourceType.IsRefundable,
                    StudentWiseDifferentAmount = sourceType.StudentWiseDifferentAmount
                };
                EnsureInsertAudit(cloneType, utcNow, actorId);
                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableFeeTypes}
                        (id, feestructureversionid, name, category, frequency, ismandatory, isrefundable,
                         studentwisedifferentamount, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureVersionId, @Name, @Category, @CollectionType, @IsMandatory, @IsRefundable,
                         @StudentWiseDifferentAmount, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    cloneType,
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);
                typeMap[sourceType.Id] = cloneType.Id;
            }

            IList<ClassFeeAmountEntity> sourceAmounts = (await connection.QueryAsync<ClassFeeAmountEntity>(new CommandDefinition(
                $"""
                SELECT id AS Id, feestructureversionid AS FeeStructureVersionId, classid AS ClassId,
                       feetypeid AS FeeTypeId, academicyearid AS AcademicYearId, amount AS Amount,
                       COALESCE(semester1amount, 0) AS Semester1Amount,
                       COALESCE(semester2amount, 0) AS Semester2Amount
                FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                WHERE feestructureversionid = @SourceVersionId AND isactive = true;
                """,
                new { SourceVersionId = sourceVersionId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false)).ToList();

            foreach (ClassFeeAmountEntity sourceAmount in sourceAmounts)
            {
                if (!typeMap.TryGetValue(sourceAmount.FeeTypeId, out Guid newFeeTypeId))
                {
                    continue;
                }

                var cloneAmount = new ClassFeeAmountEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureVersionId = newVersionId,
                    ClassId = sourceAmount.ClassId,
                    FeeTypeId = newFeeTypeId,
                    AcademicYearId = sourceAmount.AcademicYearId,
                    Amount = sourceAmount.Amount,
                    Semester1Amount = sourceAmount.Semester1Amount,
                    Semester2Amount = sourceAmount.Semester2Amount
                };
                EnsureInsertAudit(cloneAmount, utcNow, actorId);
                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                        (id, feestructureversionid, classid, feetypeid, academicyearid, amount,
                         semester1amount, semester2amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureVersionId, @ClassId, @FeeTypeId, @AcademicYearId, @Amount,
                         @Semester1Amount, @Semester2Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    cloneAmount,
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

    public async Task<IList<FeeTypeListRow>> GetFeeTypesAsync(Guid feeStructureVersionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS Id,
                   ft.feestructureversionid AS FeeStructureVersionId,
                   ft.name AS Name,
                   ft.category AS Category,
                   ft.frequency AS CollectionType,
                   ft.ismandatory AS IsMandatory,
                   COALESCE(ft.studentwisedifferentamount, false) AS StudentWiseDifferentAmount,
                   ft.isrefundable AS IsRefundable,
                   ft.isactive AS IsActive,
                   EXISTS (
                       SELECT 1
                       FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                       INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp
                           ON fp.id = fpa.paymentid AND fp.isactive = true
                       WHERE fpa.feetypeid = ft.id AND fpa.isactive = true AND fpa.amount > 0
                   ) AS HasStudentPayments
            FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
            WHERE ft.feestructureversionid = @VersionId AND ft.isactive = true
            ORDER BY ft.name;
            """;
        IEnumerable<FeeTypeListRow> rows = await connection
            .QueryAsync<FeeTypeListRow>(new CommandDefinition(
                sql,
                new { VersionId = feeStructureVersionId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<FeeTypeEntity?> GetFeeTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, feestructureversionid AS FeeStructureVersionId, name AS Name,
                   category AS Category, frequency AS CollectionType,
                   ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                   COALESCE(studentwisedifferentamount, false) AS StudentWiseDifferentAmount,
                   isactive AS IsActive,
                   versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeTypes}
            WHERE id = @Id;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeTypeEntity>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableFeeTypes}
                (id, feestructureversionid, name, category, frequency, ismandatory, isrefundable,
                 studentwisedifferentamount, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @FeeStructureVersionId, @Name, @Category, @CollectionType, @IsMandatory, @IsRefundable,
                 @StudentWiseDifferentAmount, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeTypes}
            SET name = @Name,
                category = @Category,
                frequency = @CollectionType,
                ismandatory = @IsMandatory,
                studentwisedifferentamount = @StudentWiseDifferentAmount,
                isrefundable = @IsRefundable,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteFeeTypeAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeTypes}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> FeeTypeHasPaymentsAsync(Guid feeTypeId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
                WHERE fpa.feetypeid = @FeeTypeId AND fpa.isactive = true AND fpa.amount > 0);
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { FeeTypeId = feeTypeId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveFeeTypesForVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableFeeTypes}
            WHERE feestructureversionid = @VersionId AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountClassesWithAmountsForVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(DISTINCT classid)
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
            WHERE feestructureversionid = @VersionId AND isactive = true AND amount > 0;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeSettingsEntity?> GetSettingsAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, latefeeday AS LateFeePerDay,
                   defaultacademicyearid AS DefaultAcademicYearId,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeSettings}
            WHERE isactive = true
            ORDER BY createdon
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeSettingsEntity>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> UpsertSettingsAsync(FeeSettingsEntity entity, CancellationToken ct = default)
    {
        FeeSettingsEntity? existing = await GetSettingsAsync(ct).ConfigureAwait(false);
        if (existing is null)
        {
            IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
            DateTime utcNow = DateTime.UtcNow;
            Guid actorId = ResolveInsertActor();
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            EnsureInsertAudit(entity, utcNow, actorId);
            string insertSql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableFeeSettings}
                    (id, paymentcycle, latefeeday, defaultacademicyearid,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, 0, @LateFeePerDay, @DefaultAcademicYearId,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, entity, cancellationToken: ct)).ConfigureAwait(false);
            return entity.Id;
        }

        entity.Id = existing.Id;
        entity.VersionNo = existing.VersionNo;
        IDbConnection updateConnection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string updateSql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeSettings}
            SET latefeeday = @LateFeePerDay,
                defaultacademicyearid = @DefaultAcademicYearId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id;
            """;
        await updateConnection.ExecuteAsync(new CommandDefinition(updateSql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
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
