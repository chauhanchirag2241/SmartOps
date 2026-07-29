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
        FeeStructureVersionStatus? status,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "v", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT v.id AS Id,
                   v.versionnumber AS VersionNumber,
                   v.status AS Status,
                   v.effectivedate AS EffectiveDate,
                   v.publishedon AS PublishedOn,
                   v.activatedon AS ActivatedOn,
                   (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableFeeHead} ft
                    WHERE ft.feestructureid = v.id AND ft.isactive = true) AS FeeHeadCount,
                   EXISTS (
                       SELECT 1 FROM {Schema}.{DatabaseConfig.TableFeePayments} fp
                       WHERE fp.feestructureid = v.id AND fp.isactive = true
                   ) AS HasStudentPayments
            FROM {Schema}.{DatabaseConfig.TableFeeStructure} v
            WHERE v.isactive = true{branchFilter}
            {(status.HasValue ? "AND v.status = @Status" : string.Empty)}
            ORDER BY v.versionnumber DESC;
            """;

        IEnumerable<FeeStructureVersionListRow> rows = await connection
            .QueryAsync<FeeStructureVersionListRow>(new CommandDefinition(
                sql,
                new
                {
                    Status = status.HasValue ? (short)status.Value : (short?)null,
                    ActiveBranchId = activeBranchId
                },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<FeeStructureEntity?> GetVersionByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, versionnumber AS VersionNumber,
                   status AS Status, effectivedate AS EffectiveDate, publishedon AS PublishedOn,
                   activatedon AS ActivatedOn, isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn, updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeStructure}
            WHERE id = @Id AND isactive = true;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeStructureEntity?> GetActiveFeeStructureAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "v", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT v.id AS Id, v.versionnumber AS VersionNumber,
                   v.status AS Status, v.effectivedate AS EffectiveDate, v.publishedon AS PublishedOn,
                   v.activatedon AS ActivatedOn, v.isactive AS IsActive, v.versionno AS VersionNo,
                   v.createdby AS CreatedBy, v.createdon AS CreatedOn, v.updatedby AS UpdatedBy, v.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeStructure} v
            WHERE v.status = @ActiveStatus AND v.isactive = true{branchFilter}
            ORDER BY v.versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureEntity>(new CommandDefinition(
                sql,
                new { ActiveStatus = (short)FeeStructureVersionStatus.Active, ActiveBranchId = activeBranchId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeStructureEntity?> GetAdmissionFeeStructureAsync(CancellationToken ct = default)
    {
        FeeStructureEntity? active = await GetActiveFeeStructureAsync(ct).ConfigureAwait(false);
        if (active is not null)
        {
            return active;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "v", ct)
            .ConfigureAwait(false);
        string sql = $"""
            SELECT v.id AS Id, v.versionnumber AS VersionNumber,
                   v.status AS Status, v.effectivedate AS EffectiveDate, v.publishedon AS PublishedOn,
                   v.activatedon AS ActivatedOn, v.isactive AS IsActive, v.versionno AS VersionNo,
                   v.createdby AS CreatedBy, v.createdon AS CreatedOn, v.updatedby AS UpdatedBy, v.updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeStructure} v
            WHERE v.status = @PublishedStatus
              AND v.isactive = true{branchFilter}
            ORDER BY v.versionnumber DESC
            LIMIT 1;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeStructureEntity>(new CommandDefinition(
                sql,
                new { PublishedStatus = (short)FeeStructureVersionStatus.Published, ActiveBranchId = activeBranchId },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> GetNextVersionNumberAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid branchId = await _branchWrite.ResolveWriteBranchIdAsync(Guid.Empty, ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COALESCE(MAX(versionnumber), 0) + 1
            FROM {Schema}.{DatabaseConfig.TableFeeStructure}
            WHERE branchid = @BranchId;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { BranchId = branchId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateVersionAsync(FeeStructureEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(entity.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableFeeStructure}
                (id, branchid, versionnumber, status, effectivedate, publishedon, activatedon,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @VersionNumber, @Status, @EffectiveDate, @PublishedOn, @ActivatedOn,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateVersionAsync(FeeStructureEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructure}
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
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructure}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task ArchiveActiveStructuresAsync(Guid exceptVersionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructure} v
            SET status = @ArchivedStatus,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = v.versionno + 1
            WHERE v.branchid = (
                      SELECT branchid FROM {Schema}.{DatabaseConfig.TableFeeStructure} WHERE id = @ExceptVersionId
                  )
              AND v.id <> @ExceptVersionId
              AND v.status = @ActiveStatus
              AND v.isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ExceptVersionId = exceptVersionId,
                ActiveStatus = (short)FeeStructureVersionStatus.Active,
                ArchivedStatus = (short)FeeStructureVersionStatus.Archived,
                UpdatedBy = actorId,
                UpdatedOn = utcNow
            },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task ArchivePublishedStructuresAsync(Guid exceptVersionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeStructure} v
            SET status = @ArchivedStatus,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = v.versionno + 1
            WHERE v.branchid = (
                      SELECT branchid FROM {Schema}.{DatabaseConfig.TableFeeStructure} WHERE id = @ExceptVersionId
                  )
              AND v.id <> @ExceptVersionId
              AND v.status = @PublishedStatus
              AND v.isactive = true;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
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
                WHERE feestructureid = @VersionId AND isactive = true);
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
                WHERE feestructureid = @VersionId AND isactive = true);
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

            IList<FeeHeadEntity> sourceTypes = (await connection.QueryAsync<FeeHeadEntity>(new CommandDefinition(
                $"""
                SELECT id AS Id, feestructureid AS FeeStructureId, name AS Name,
                       category AS Category, frequency AS CollectionType,
                       ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                       COALESCE(studentwisedifferentamount, false) AS StudentWiseDifferentAmount,
                       isactive AS IsActive
                FROM {Schema}.{DatabaseConfig.TableFeeHead}
                WHERE feestructureid = @SourceVersionId AND isactive = true;
                """,
                new { SourceVersionId = sourceVersionId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false)).ToList();

            var typeMap = new Dictionary<Guid, Guid>();
            foreach (FeeHeadEntity sourceType in sourceTypes)
            {
                var cloneType = new FeeHeadEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureId = newVersionId,
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
                    INSERT INTO {Schema}.{DatabaseConfig.TableFeeHead}
                        (id, feestructureid, name, category, frequency, ismandatory, isrefundable,
                         studentwisedifferentamount, isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureId, @Name, @Category, @CollectionType, @IsMandatory, @IsRefundable,
                         @StudentWiseDifferentAmount, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    cloneType,
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);
                typeMap[sourceType.Id] = cloneType.Id;
            }

            IList<ClassFeeAmountEntity> sourceAmounts = (await connection.QueryAsync<ClassFeeAmountEntity>(new CommandDefinition(
                $"""
                SELECT id AS Id, feestructureid AS FeeStructureId, classgroupid AS ClassGroupId,
                       feeheadid AS FeeHeadId, academicyearid AS AcademicYearId, amount AS Amount
                FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                WHERE feestructureid = @SourceVersionId AND isactive = true;
                """,
                new { SourceVersionId = sourceVersionId },
                transaction,
                cancellationToken: ct)).ConfigureAwait(false)).ToList();

            foreach (ClassFeeAmountEntity sourceAmount in sourceAmounts)
            {
                if (!typeMap.TryGetValue(sourceAmount.FeeHeadId, out Guid newFeeHeadId))
                {
                    continue;
                }

                var cloneAmount = new ClassFeeAmountEntity
                {
                    Id = Guid.NewGuid(),
                    FeeStructureId = newVersionId,
                    ClassGroupId = sourceAmount.ClassGroupId,
                    FeeHeadId = newFeeHeadId,
                    AcademicYearId = sourceAmount.AcademicYearId,
                    Amount = sourceAmount.Amount,
                };
                EnsureInsertAudit(cloneAmount, utcNow, actorId);
                await connection.ExecuteAsync(new CommandDefinition(
                    $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                        (id, feestructureid, classgroupid, feeheadid, academicyearid, amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @FeeStructureId, @ClassGroupId, @FeeHeadId, @AcademicYearId, @Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """,
                    cloneAmount,
                    transaction,
                    cancellationToken: ct)).ConfigureAwait(false);

                List<ClassFeePeriodAmountEntity> sourcePeriodAmounts = (await connection
                    .QueryAsync<ClassFeePeriodAmountEntity>(new CommandDefinition(
                        $"""
                        SELECT id AS Id,
                               classfeeamountid AS ClassFeeAmountId,
                               periodindex AS PeriodIndex,
                               amount AS Amount
                        FROM {Schema}.{DatabaseConfig.TableClassFeePeriodAmounts}
                        WHERE classfeeamountid = @ClassFeeAmountId AND isactive = true;
                        """,
                        new { ClassFeeAmountId = sourceAmount.Id },
                        transaction,
                        cancellationToken: ct))
                    .ConfigureAwait(false)).ToList();
                foreach (ClassFeePeriodAmountEntity sourcePeriodAmount in sourcePeriodAmounts)
                {
                    var clonePeriodAmount = new ClassFeePeriodAmountEntity
                    {
                        Id = Guid.NewGuid(),
                        ClassFeeAmountId = cloneAmount.Id,
                        PeriodIndex = sourcePeriodAmount.PeriodIndex,
                        Amount = sourcePeriodAmount.Amount,
                    };
                    EnsureInsertAudit(clonePeriodAmount, utcNow, actorId);
                    await InsertAsync(
                        connection,
                        Schema,
                        DatabaseConfig.TableClassFeePeriodAmounts,
                        clonePeriodAmount,
                        transaction).ConfigureAwait(false);
                }
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

    public async Task<IList<FeeHeadListRow>> GetFeeHeadsAsync(Guid feeStructureId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS Id,
                   ft.feestructureid AS FeeStructureId,
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
                       WHERE fpa.feeheadid = ft.id AND fpa.isactive = true AND fpa.amount > 0
                   ) AS HasStudentPayments
            FROM {Schema}.{DatabaseConfig.TableFeeHead} ft
            WHERE ft.feestructureid = @VersionId AND ft.isactive = true
            ORDER BY ft.name;
            """;
        IEnumerable<FeeHeadListRow> rows = await connection
            .QueryAsync<FeeHeadListRow>(new CommandDefinition(
                sql,
                new { VersionId = feeStructureId },
                cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<FeeHeadEntity?> GetFeeHeadByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, feestructureid AS FeeStructureId, name AS Name,
                   category AS Category, frequency AS CollectionType,
                   ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                   COALESCE(studentwisedifferentamount, false) AS StudentWiseDifferentAmount,
                   isactive AS IsActive,
                   versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeHead}
            WHERE id = @Id;
            """;
        return await connection
            .QueryFirstOrDefaultAsync<FeeHeadEntity>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateFeeHeadAsync(FeeHeadEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableFeeHead}
                (id, feestructureid, name, category, frequency, ismandatory, isrefundable,
                 studentwisedifferentamount, isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @FeeStructureId, @Name, @Category, @CollectionType, @IsMandatory, @IsRefundable,
                 @StudentWiseDifferentAmount, @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateFeeHeadAsync(FeeHeadEntity entity, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeHead}
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

    public async Task SoftDeleteFeeHeadAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeHead}
            SET isactive = false, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> FeeHeadHasPaymentsAsync(Guid feeHeadId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {Schema}.{DatabaseConfig.TableFeePaymentAllocations} fpa
                INNER JOIN {Schema}.{DatabaseConfig.TableFeePayments} fp ON fp.id = fpa.paymentid AND fp.isactive = true
                WHERE fpa.feeheadid = @FeeHeadId AND fpa.isactive = true AND fpa.amount > 0);
            """;
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { FeeHeadId = feeHeadId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveFeeHeadsForStructureAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableFeeHead}
            WHERE feestructureid = @VersionId AND isactive = true;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountClassesWithAmountsForVersionAsync(Guid versionId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(DISTINCT classgroupid)
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
            WHERE feestructureid = @VersionId AND isactive = true AND amount > 0;
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { VersionId = versionId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
