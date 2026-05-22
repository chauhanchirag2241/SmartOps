using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class FeeStructureRepository : BaseRepository, IFeeStructureRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    public FeeStructureRepository(
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

    public async Task<IList<FeeTypeEntity>> GetFeeTypesAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, category AS Category, frequency AS Frequency,
                   ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
                   updatedby AS UpdatedBy, updatedon AS UpdatedOn
            FROM {Schema}.{DatabaseConfig.TableFeeTypes}
            WHERE isactive = true
            ORDER BY name;
            """;
        IEnumerable<FeeTypeEntity> rows = await connection
            .QueryAsync<FeeTypeEntity>(new CommandDefinition(sql, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<FeeTypeEntity?> GetFeeTypeByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, name AS Name, category AS Category, frequency AS Frequency,
                   ismandatory AS IsMandatory, isrefundable AS IsRefundable,
                   isactive AS IsActive, versionno AS VersionNo,
                   createdby AS CreatedBy, createdon AS CreatedOn,
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
                (id, name, category, frequency, ismandatory, isrefundable,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @Name, @Category, @Frequency, @IsMandatory, @IsRefundable,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
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
                frequency = @Frequency,
                ismandatory = @IsMandatory,
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

    public async Task SetFeeTypeActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        Guid actorId = ResolveInsertActor();
        DateTime utcNow = DateTime.UtcNow;
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeTypes}
            SET isactive = @IsActive, updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, IsActive = isActive, UpdatedBy = actorId, UpdatedOn = utcNow }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<int> CountActiveFeeTypesAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"SELECT COUNT(*) FROM {Schema}.{DatabaseConfig.TableFeeTypes} WHERE isactive = true;";
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<int> CountClassesWithAmountsAsync(Guid? academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT COUNT(DISTINCT classid)
            FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
            WHERE isactive = true AND amount > 0
            {(academicYearId.HasValue ? "AND academicyearid = @AcademicYearId" : string.Empty)};
            """;
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<FeeSettingsEntity?> GetSettingsAsync(CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT id AS Id, paymentcycle AS PaymentCycle, latefeeday AS LateFeePerDay,
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
            return await CreateFeeTypeSettingsAsync(entity, ct).ConfigureAwait(false);
        }

        entity.Id = existing.Id;
        entity.VersionNo = existing.VersionNo;
        await UpdateSettingsAsync(entity, ct).ConfigureAwait(false);
        return entity.Id;
    }

    private async Task<Guid> CreateFeeTypeSettingsAsync(FeeSettingsEntity entity, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        EnsureInsertAudit(entity, utcNow, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableFeeSettings}
                (id, paymentcycle, latefeeday, defaultacademicyearid,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @PaymentCycle, @LateFeePerDay, @DefaultAcademicYearId,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
        return entity.Id;
    }

    private async Task UpdateSettingsAsync(FeeSettingsEntity entity, CancellationToken ct)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(entity, ResolveInsertActor(), DateTime.UtcNow);
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableFeeSettings}
            SET paymentcycle = @PaymentCycle,
                latefeeday = @LateFeePerDay,
                defaultacademicyearid = @DefaultAcademicYearId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: ct)).ConfigureAwait(false);
    }
}
