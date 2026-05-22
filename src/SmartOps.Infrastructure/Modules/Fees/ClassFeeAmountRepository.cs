using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Fees.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Fees;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Fees;

public sealed class ClassFeeAmountRepository : BaseRepository, IClassFeeAmountRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;

    private const string ClassDisplayNameSql =
        "c.classname || CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    public ClassFeeAmountRepository(
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

    public async Task<IList<ClassFeeSummaryRow>> GetClassSummariesAsync(Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT c.id AS ClassId,
                   {ClassDisplayNameSql} AS ClassName,
                   (SELECT COUNT(*)::int FROM {Schema}.{DatabaseConfig.TableStudentAcademics} sa
                    INNER JOIN {Schema}.{DatabaseConfig.TableStudents} s ON s.id = sa.studentid AND s.isactive = true
                    WHERE sa.classid = c.id AND sa.academicyearid = @AcademicYearId AND sa.isactive = true) AS StudentCount,
                   COALESCE((
                       SELECT SUM(cfa.amount)
                       FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                       INNER JOIN {Schema}.{DatabaseConfig.TableFeeTypes} ft ON ft.id = cfa.feetypeid AND ft.isactive = true
                       WHERE cfa.classid = c.id AND cfa.academicyearid = @AcademicYearId AND cfa.isactive = true
                   ), 0) AS TotalAmount
            FROM {Schema}.{DatabaseConfig.TableClasses} c
            WHERE c.academicyearid = @AcademicYearId AND c.isactive = true
            ORDER BY c.classname, c.section;
            """;
        IEnumerable<ClassFeeSummaryRow> rows = await connection
            .QueryAsync<ClassFeeSummaryRow>(new CommandDefinition(sql, new { AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ClassFeeAmountRow>> GetAmountsByClassAsync(Guid classId, Guid academicYearId, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT ft.id AS FeeTypeId,
                   ft.name AS FeeTypeName,
                   ft.category AS Category,
                   ft.frequency AS Frequency,
                   COALESCE(cfa.amount, 0) AS Amount
            FROM {Schema}.{DatabaseConfig.TableFeeTypes} ft
            LEFT JOIN {Schema}.{DatabaseConfig.TableClassFeeAmounts} cfa
                ON cfa.feetypeid = ft.id AND cfa.classid = @ClassId AND cfa.academicyearid = @AcademicYearId AND cfa.isactive = true
            WHERE ft.isactive = true
            ORDER BY ft.name;
            """;
        IEnumerable<ClassFeeAmountRow> rows = await connection
            .QueryAsync<ClassFeeAmountRow>(new CommandDefinition(sql, new { ClassId = classId, AcademicYearId = academicYearId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task UpsertAmountsAsync(
        Guid classId,
        Guid academicYearId,
        IList<(Guid FeeTypeId, decimal Amount)> amounts,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime utcNow = DateTime.UtcNow;
        Guid actorId = ResolveInsertActor();

        foreach ((Guid feeTypeId, decimal amount) in amounts)
        {
            string existsSql = $"""
                SELECT id FROM {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                WHERE classid = @ClassId AND feetypeid = @FeeTypeId AND academicyearid = @AcademicYearId;
                """;
            Guid? existingId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(existsSql, new { ClassId = classId, FeeTypeId = feeTypeId, AcademicYearId = academicYearId }, cancellationToken: ct))
                .ConfigureAwait(false);

            if (existingId.HasValue)
            {
                string updateSql = $"""
                    UPDATE {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                    SET amount = @Amount, isactive = true,
                        updatedby = @UpdatedBy, updatedon = @UpdatedOn, versionno = versionno + 1
                    WHERE id = @Id;
                    """;
                await connection.ExecuteAsync(new CommandDefinition(
                    updateSql,
                    new { Id = existingId.Value, Amount = amount, UpdatedBy = actorId, UpdatedOn = utcNow },
                    cancellationToken: ct)).ConfigureAwait(false);
            }
            else
            {
                var entity = new ClassFeeAmountEntity
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    FeeTypeId = feeTypeId,
                    AcademicYearId = academicYearId,
                    Amount = amount
                };
                EnsureInsertAudit(entity, utcNow, actorId);
                string insertSql = $"""
                    INSERT INTO {Schema}.{DatabaseConfig.TableClassFeeAmounts}
                        (id, classid, feetypeid, academicyearid, amount,
                         isactive, versionno, createdby, createdon, updatedby, updatedon)
                    VALUES
                        (@Id, @ClassId, @FeeTypeId, @AcademicYearId, @Amount,
                         @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                    """;
                await connection.ExecuteAsync(new CommandDefinition(insertSql, entity, cancellationToken: ct))
                    .ConfigureAwait(false);
            }
        }
    }
}
