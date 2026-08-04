using System.Data;
using System.Text;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Exam;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Exam;

public sealed class ExamRepository : BaseRepository, IExamRepository
{
    private readonly ITenantSchemaProvider _tenantSchema;
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public ExamRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        ITenantSchemaProvider tenantSchema,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _tenantSchema = tenantSchema;
        _scope = scope;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    private string Schema =>
        _tenantSchema.IsTenantScoped
            ? _tenantSchema.GetOperationalSchema()
            : DatabaseConfig.Schema_School;

    private static string YearFilter(string alias) =>
        $" AND (@ScopeAcademicYearId IS NULL OR {alias}.academicyearid = @ScopeAcademicYearId)";

    // ── Grade scales ─────────────────────────────────────────

    public async Task<IList<ExamGradeScaleEntity>> GetGradeScalesAsync(CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "g", ct)
            .ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT g.id, g.branchid, g.name, g.description, g.isdefault,
                   g.isactive, g.versionno, g.createdby, g.createdon, g.updatedby, g.updatedon
            FROM {Schema}.{DatabaseConfig.TableExamGradeScales} g
            WHERE g.isactive = true{branchFilter}
            ORDER BY g.isdefault DESC, g.name;
            """;

        IEnumerable<ExamGradeScaleEntity> rows = await connection.QueryAsync<ExamGradeScaleEntity>(
                new CommandDefinition(sql, new { ActiveBranchId = activeBranchId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ExamGradeScaleEntity?> GetGradeScaleByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, branchid, name, description, isdefault,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamGradeScales}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExamGradeScaleEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ExamGradeScaleDetailEntity>> GetGradeScaleDetailsAsync(
        IReadOnlyCollection<Guid> scaleIds,
        CancellationToken ct = default)
    {
        if (scaleIds.Count == 0)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, gradescaleid, grade, minpercent, maxpercent, gradepoint, description, displayorder,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamGradeScaleDetails}
            WHERE gradescaleid = ANY(@ScaleIds) AND isactive = true
            ORDER BY displayorder, maxpercent DESC;
            """;

        IEnumerable<ExamGradeScaleDetailEntity> rows = await connection.QueryAsync<ExamGradeScaleDetailEntity>(
                new CommandDefinition(sql, new { ScaleIds = scaleIds.ToArray() }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid> CreateGradeScaleAsync(
        ExamGradeScaleEntity scale,
        IList<ExamGradeScaleDetailEntity> details,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        scale.Id = scale.Id == Guid.Empty ? Guid.NewGuid() : scale.Id;
        scale.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(scale.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(scale, now, actorId);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string sql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableExamGradeScales}
                    (id, branchid, name, description, isdefault,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @BranchId, @Name, @Description, @IsDefault,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, scale, tx, cancellationToken: ct)).ConfigureAwait(false);

            if (scale.IsDefault)
            {
                await ClearOtherDefaultsAsync(conn, tx, scale.Id, scale.BranchId, actorId, now, ct).ConfigureAwait(false);
            }

            await InsertGradeScaleDetailsAsync(conn, tx, scale.Id, details, actorId, now, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return scale.Id;
    }

    public async Task UpdateGradeScaleAsync(
        ExamGradeScaleEntity scale,
        IList<ExamGradeScaleDetailEntity> details,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(scale, actorId, now);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string sql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExamGradeScales}
                SET name = @Name,
                    description = @Description,
                    isdefault = @IsDefault,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE id = @Id AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, scale, tx, cancellationToken: ct)).ConfigureAwait(false);

            if (scale.IsDefault)
            {
                await ClearOtherDefaultsAsync(conn, tx, scale.Id, scale.BranchId, actorId, now, ct).ConfigureAwait(false);
            }

            // Replace grade rows (dynamic add/remove).
            string deactivateSql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExamGradeScaleDetails}
                SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
                WHERE gradescaleid = @ScaleId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    deactivateSql,
                    new { ScaleId = scale.Id, ActorId = actorId, Now = now },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);

            await InsertGradeScaleDetailsAsync(conn, tx, scale.Id, details, actorId, now, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task ClearOtherDefaultsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid keepId,
        Guid branchId,
        Guid actorId,
        DateTime now,
        CancellationToken ct)
    {
        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamGradeScales}
            SET isdefault = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE id <> @KeepId AND branchid = @BranchId AND isdefault = true AND isactive = true;
            """;
        await conn.ExecuteAsync(new CommandDefinition(
                sql,
                new { KeepId = keepId, BranchId = branchId, ActorId = actorId, Now = now },
                tx,
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task InsertGradeScaleDetailsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid scaleId,
        IList<ExamGradeScaleDetailEntity> details,
        Guid actorId,
        DateTime now,
        CancellationToken ct)
    {
        foreach (ExamGradeScaleDetailEntity detail in details)
        {
            detail.Id = Guid.NewGuid();
            detail.GradeScaleId = scaleId;
            EnsureInsertAudit(detail, now, actorId);

            string sql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableExamGradeScaleDetails}
                    (id, gradescaleid, grade, minpercent, maxpercent, gradepoint, description, displayorder,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @GradeScaleId, @Grade, @MinPercent, @MaxPercent, @GradePoint, @Description, @DisplayOrder,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, detail, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
    }

    public async Task SoftDeleteGradeScaleAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamGradeScales}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableExamGradeScaleDetails}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE gradescaleid = @Id;
            """;

        await connection.ExecuteAsync(
                new CommandDefinition(sql, new { Id = id, ActorId = actorId, Now = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> GradeScaleInUseAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableExamGroups}
                WHERE gradescaleid = @Id AND isactive = true
            ) OR EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableExams}
                WHERE gradescaleid = @Id AND isactive = true
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    // ── Exam groups ──────────────────────────────────────────

    public async Task<IList<ExamGroupRow>> GetGroupsAsync(CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        (string branchFilter, Guid? activeBranchId) = await BranchSqlBuilder
            .GetActiveBranchFilterAsync(_branchContext, "g", ct)
            .ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("ActiveBranchId", activeBranchId);

        var scopeFilter = new StringBuilder();
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count == 0)
            {
                return [];
            }

            scopeFilter.Append($"""
                 AND EXISTS (
                    SELECT 1
                    FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings} m
                    INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c
                        ON c.classgroupid = m.classgroupid AND c.isactive = true
                    WHERE m.examgroupid = g.id
                      AND m.isactive = true
                      AND c.id = ANY(@ScopeClassIds))
                """);
            parameters.Add("ScopeClassIds", _scope.AllowedClassIds.ToArray());
        }

        string sql = $"""
            SELECT g.id AS Id,
                   g.name AS Name,
                   g.description AS Description,
                   g.gradescaleid AS GradeScaleId,
                   gs.name AS GradeScaleName,
                   g.evaluationtype::int AS EvaluationType,
                   COALESCE(COUNT(DISTINCT e.id), 0)::int AS ExamCount,
                   COALESCE((
                       SELECT string_agg(cg.classname, ', ' ORDER BY cg.classname)
                       FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings} m2
                       INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg
                           ON cg.id = m2.classgroupid AND cg.isactive = true
                       WHERE m2.examgroupid = g.id AND m2.isactive = true
                   ), '') AS ClassGroupNames
            FROM {Schema}.{DatabaseConfig.TableExamGroups} g
            LEFT JOIN {Schema}.{DatabaseConfig.TableExamGradeScales} gs ON gs.id = g.gradescaleid AND gs.isactive = true
            LEFT JOIN {Schema}.{DatabaseConfig.TableExams} e ON e.examgroupid = g.id AND e.isactive = true
            WHERE g.isactive = true{branchFilter}{scopeFilter}
            GROUP BY g.id, g.name, g.description, g.gradescaleid, gs.name, g.evaluationtype, g.createdon
            ORDER BY g.createdon DESC;
            """;

        IEnumerable<ExamGroupRow> rows = await connection.QueryAsync<ExamGroupRow>(
                new CommandDefinition(sql, parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ExamGroupEntity?> GetGroupByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, branchid, name, description, gradescaleid, evaluationtype,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamGroups}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExamGroupEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateGroupAsync(ExamGroupEntity group, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        group.Id = group.Id == Guid.Empty ? Guid.NewGuid() : group.Id;
        group.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(group.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(group, now, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableExamGroups}
                (id, branchid, name, description, gradescaleid, evaluationtype,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @BranchId, @Name, @Description, @GradeScaleId, @EvaluationType,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, group, cancellationToken: ct)).ConfigureAwait(false);
        return group.Id;
    }

    public async Task UpdateGroupAsync(ExamGroupEntity group, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(group, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamGroups}
            SET name = @Name,
                description = @Description,
                gradescaleid = @GradeScaleId,
                evaluationtype = @EvaluationType,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, group, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            await SoftDeleteRelatedAsync(
                    conn,
                    Schema,
                    DatabaseConfig.TableClassGroupExamMappings,
                    "examgroupid",
                    id,
                    tx)
                .ConfigureAwait(false);

            string sql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExamGroups}
                SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
                WHERE id = @Id;
                """;

            await conn.ExecuteAsync(
                    new CommandDefinition(sql, new { Id = id, ActorId = actorId, Now = now }, tx, cancellationToken: ct))
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetClassGroupIdsForExamGroupAsync(
        Guid examGroupId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT classgroupid
            FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings}
            WHERE examgroupid = @ExamGroupId AND isactive = true
            ORDER BY createdon ASC;
            """;
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(
                new CommandDefinition(sql, new { ExamGroupId = examGroupId }, cancellationToken: ct))
            .ConfigureAwait(false);
        return ids.ToList();
    }

    public async Task<bool> AllClassesBelongToExamGroupAsync(
        Guid examGroupId,
        IReadOnlyList<Guid> classIds,
        CancellationToken ct = default)
    {
        Guid[] ids = (classIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return true;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
SELECT COUNT(1) = @ExpectedCount
FROM {Schema}.{DatabaseConfig.TableClasses} c
WHERE c.id = ANY(@ClassIds)
  AND c.isactive = true
  AND c.classgroupid IS NOT NULL
  AND EXISTS (
      SELECT 1
      FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings} m
      WHERE m.examgroupid = @ExamGroupId
        AND m.classgroupid = c.classgroupid
        AND m.isactive = true
  );
""";
        return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new { ExamGroupId = examGroupId, ClassIds = ids, ExpectedCount = ids.Length },
                    cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetClassGroupIdsByExamGroupAsync(
        IReadOnlyCollection<Guid> examGroupIds,
        CancellationToken ct = default)
    {
        if (examGroupIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string sql = $"""
            SELECT examgroupid AS ExamGroupId, classgroupid AS ClassGroupId
            FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings}
            WHERE examgroupid = ANY(@ExamGroupIds) AND isactive = true
            ORDER BY createdon ASC;
            """;

        IEnumerable<(Guid ExamGroupId, Guid ClassGroupId)> rows = await connection
            .QueryAsync<(Guid ExamGroupId, Guid ClassGroupId)>(
                new CommandDefinition(sql, new { ExamGroupIds = examGroupIds.ToArray() }, cancellationToken: ct))
            .ConfigureAwait(false);

        return rows
            .GroupBy(r => r.ExamGroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(x => x.ClassGroupId).ToList());
    }

    public async Task SaveExamGroupClassGroupIdsAsync(
        Guid examGroupId,
        Guid branchId,
        IReadOnlyList<Guid> classGroupIds,
        bool allowRemove,
        CancellationToken ct = default)
    {
        DateTime now = SchoolLocalTime.NowDateTime();
        List<Guid> desired = (classGroupIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        string table = DatabaseConfig.TableClassGroupExamMappings;
        Guid resolvedBranch = await _branchWrite.ResolveWriteBranchIdAsync(branchId, ct).ConfigureAwait(false);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            List<(Guid Id, Guid ClassGroupId)> existing = (await conn.QueryAsync<(Guid Id, Guid ClassGroupId)>(
                    new CommandDefinition(
                        $"""
                        SELECT id AS Id, classgroupid AS ClassGroupId
                        FROM {Schema}.{table}
                        WHERE examgroupid = @ExamGroupId AND isactive = true;
                        """,
                        new { ExamGroupId = examGroupId },
                        transaction: tx,
                        cancellationToken: ct))
                .ConfigureAwait(false)).ToList();

            Dictionary<Guid, Guid> existingByGroup = existing.ToDictionary(x => x.ClassGroupId, x => x.Id);
            List<Guid> toAdd = desired.Where(id => !existingByGroup.ContainsKey(id)).ToList();

            if (allowRemove)
            {
                List<Guid> toRemove = existing
                    .Where(x => !desired.Contains(x.ClassGroupId))
                    .Select(x => x.Id)
                    .ToList();
                foreach (Guid rowId in toRemove)
                {
                    await SoftDeleteAsync(conn, Schema, table, rowId, tx).ConfigureAwait(false);
                }
            }

            foreach (Guid classGroupId in toAdd)
            {
                var row = new ClassGroupExamMappingEntity
                {
                    Id = Guid.NewGuid(),
                    BranchId = resolvedBranch,
                    ExamGroupId = examGroupId,
                    ClassGroupId = classGroupId,
                };
                EnsureInsertAudit(row, now);
                await InsertAsync(conn, Schema, table, row, tx).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> GroupHasExamsAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableExams}
                WHERE examgroupid = @Id AND isactive = true
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    // ── Exams ────────────────────────────────────────────────

    public async Task<IList<ExamRow>> GetExamsAsync(
        Guid? groupId,
        Guid? classId,
        int? status,
        string? search,
        CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        var where = new StringBuilder($"e.isactive = true{YearFilter("e")}");
        var parameters = new DynamicParameters();
        parameters.Add("ScopeAcademicYearId", _scope.ActiveAcademicYearId);
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, where, parameters, "e", ct)
            .ConfigureAwait(false);

        if (groupId.HasValue && groupId.Value != Guid.Empty)
        {
            where.Append(" AND e.examgroupid = @GroupId");
            parameters.Add("GroupId", groupId.Value);
        }

        if (classId.HasValue && classId.Value != Guid.Empty)
        {
            where.Append($" AND EXISTS (SELECT 1 FROM {Schema}.{DatabaseConfig.TableExamClasses} xc WHERE xc.examid = e.id AND xc.classid = @ClassId AND xc.isactive = true)");
            parameters.Add("ClassId", classId.Value);
        }

        if (status.HasValue)
        {
            where.Append(" AND e.status = @Status");
            parameters.Add("Status", status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Append(" AND (e.name ILIKE @Search OR e.examtype ILIKE @Search)");
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count == 0)
            {
                return [];
            }

            // Teacher sees an exam when they have a section on the exam, or (for exams
            // still without classes) when their sections fall under the exam group's mapped class groups.
            where.Append($"""
                 AND (
                    EXISTS (
                        SELECT 1
                        FROM {Schema}.{DatabaseConfig.TableExamClasses} xc
                        WHERE xc.examid = e.id
                          AND xc.isactive = true
                          AND xc.classid = ANY(@ScopeClassIds))
                    OR (
                        NOT EXISTS (
                            SELECT 1
                            FROM {Schema}.{DatabaseConfig.TableExamClasses} xc2
                            WHERE xc2.examid = e.id AND xc2.isactive = true)
                        AND EXISTS (
                            SELECT 1
                            FROM {Schema}.{DatabaseConfig.TableClassGroupExamMappings} m
                            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c
                                ON c.classgroupid = m.classgroupid AND c.isactive = true
                            WHERE m.examgroupid = e.examgroupid
                              AND m.isactive = true
                              AND c.id = ANY(@ScopeClassIds)))
                 )
                """);
            parameters.Add("ScopeClassIds", _scope.AllowedClassIds.ToArray());
        }

        string sql = $"""
            SELECT e.id AS Id,
                   e.name AS Name,
                   e.examtype AS ExamType,
                   e.examgroupid AS ExamGroupId,
                   COALESCE(g.name, '') AS ExamGroupName,
                   e.academicperiodid AS AcademicPeriodId,
                   e.minpasspercent AS MinPassPercent,
                   e.gradescaleid AS GradeScaleId,
                   e.status::int AS Status,
                   e.resultdeclared AS ResultDeclared,
                   e.description AS Description,
                   COALESCE((SELECT SUM(mc.maxmarks) FROM {Schema}.{DatabaseConfig.TableExamMarkComponents} mc
                             WHERE mc.examid = e.id AND mc.isactive = true), 0) AS TotalMaxMarks,
                   COALESCE((SELECT COUNT(DISTINCT es.subjectid) FROM {Schema}.{DatabaseConfig.TableExamSchedules} es
                             WHERE es.examid = e.id AND es.isactive = true), 0)::int AS SubjectCount
            FROM {Schema}.{DatabaseConfig.TableExams} e
            LEFT JOIN {Schema}.{DatabaseConfig.TableExamGroups} g ON g.id = e.examgroupid
            WHERE {where}
            ORDER BY e.createdon DESC;
            """;

        IEnumerable<ExamRow> rows = await connection.QueryAsync<ExamRow>(
                new CommandDefinition(sql, parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ExamEntity?> GetExamByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examgroupid, branchid, academicyearid, name, examtype, academicperiodid,
                   minpasspercent, gradescaleid, status, resultdeclared,
                   resultdeclaredon, resultdeclaredby, description,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExams}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExamEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<IList<ExamClassRow>> GetExamClassesAsync(
        IReadOnlyCollection<Guid> examIds,
        CancellationToken ct = default)
    {
        if (examIds.Count == 0)
        {
            return [];
        }

        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("ExamIds", examIds.ToArray());
        string scopeFilter = string.Empty;
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count == 0)
            {
                return [];
            }

            scopeFilter = " AND xc.classid = ANY(@ScopeClassIds)";
            parameters.Add("ScopeClassIds", _scope.AllowedClassIds.ToArray());
        }

        string sql = $"""
            SELECT xc.examid AS ExamId,
                   xc.classid AS ClassId,
                   COALESCE({DashboardClassLabel.DisplayNameSql}, '') AS ClassName,
                   c.classgroupid AS ClassGroupId,
                   COALESCE(cg.classname, '') AS ClassGroupName
            FROM {Schema}.{DatabaseConfig.TableExamClasses} xc
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = xc.classid
            INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            WHERE xc.examid = ANY(@ExamIds) AND xc.isactive = true{scopeFilter}
            ORDER BY cg.classname, c.section;
            """;

        IEnumerable<ExamClassRow> rows = await connection.QueryAsync<ExamClassRow>(
                new CommandDefinition(sql, parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IList<ExamMarkComponentEntity>> GetComponentsAsync(
        IReadOnlyCollection<Guid> examIds,
        CancellationToken ct = default)
    {
        if (examIds.Count == 0)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examid, name, maxmarks, passingmarks, displayorder,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamMarkComponents}
            WHERE examid = ANY(@ExamIds) AND isactive = true
            ORDER BY displayorder, createdon;
            """;

        IEnumerable<ExamMarkComponentEntity> rows = await connection.QueryAsync<ExamMarkComponentEntity>(
                new CommandDefinition(sql, new { ExamIds = examIds.ToArray() }, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid> CreateExamAsync(
        ExamEntity exam,
        IList<Guid> classIds,
        IList<ExamMarkComponentEntity> components,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        exam.Id = exam.Id == Guid.Empty ? Guid.NewGuid() : exam.Id;
        exam.BranchId = await _branchWrite.ResolveWriteBranchIdAsync(exam.BranchId, ct).ConfigureAwait(false);
        EnsureInsertAudit(exam, now, actorId);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string sql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableExams}
                    (id, examgroupid, branchid, academicyearid, name, examtype, academicperiodid,
                     minpasspercent, gradescaleid, status, resultdeclared, description,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @ExamGroupId, @BranchId, @AcademicYearId, @Name, @ExamType, @AcademicPeriodId,
                     @MinPassPercent, @GradeScaleId, @Status, @ResultDeclared, @Description,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, exam, tx, cancellationToken: ct)).ConfigureAwait(false);

            await InsertExamClassesAsync(conn, tx, exam.Id, classIds, actorId, now, ct).ConfigureAwait(false);
            await InsertComponentsAsync(conn, tx, exam.Id, components, actorId, now, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return exam.Id;
    }

    public async Task UpdateExamAsync(
        ExamEntity exam,
        IList<Guid> classIds,
        IList<ExamMarkComponentEntity> components,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();
        ApplyUpdateAudit(exam, actorId, now);

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            string sql = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExams}
                SET examgroupid = @ExamGroupId,
                    name = @Name,
                    examtype = @ExamType,
                    academicperiodid = @AcademicPeriodId,
                    minpasspercent = @MinPassPercent,
                    gradescaleid = @GradeScaleId,
                    description = @Description,
                    updatedby = @UpdatedBy,
                    updatedon = @UpdatedOn,
                    versionno = versionno + 1
                WHERE id = @Id AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, exam, tx, cancellationToken: ct)).ConfigureAwait(false);

            string deactivateClasses = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExamClasses}
                SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
                WHERE examid = @ExamId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    deactivateClasses,
                    new { ExamId = exam.Id, ActorId = actorId, Now = now },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);

            string deactivateComponents = $"""
                UPDATE {Schema}.{DatabaseConfig.TableExamMarkComponents}
                SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
                WHERE examid = @ExamId AND isactive = true;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    deactivateComponents,
                    new { ExamId = exam.Id, ActorId = actorId, Now = now },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);

            await InsertExamClassesAsync(conn, tx, exam.Id, classIds, actorId, now, ct).ConfigureAwait(false);
            await InsertComponentsAsync(conn, tx, exam.Id, components, actorId, now, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private async Task InsertExamClassesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid examId,
        IList<Guid> classIds,
        Guid actorId,
        DateTime now,
        CancellationToken ct)
    {
        foreach (Guid classId in classIds.Distinct())
        {
            // Reactivate a soft-deleted mapping if present, else insert (unique examid+classid).
            string sql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableExamClasses}
                    (id, examid, classid, isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (gen_random_uuid(), @ExamId, @ClassId, true, 1, @ActorId, @Now, @ActorId, @Now)
                ON CONFLICT ON CONSTRAINT uq_examclasses_exam_class
                DO UPDATE SET isactive = true, updatedby = @ActorId, updatedon = @Now,
                              versionno = {DatabaseConfig.TableExamClasses}.versionno + 1;
                """;
            await conn.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { ExamId = examId, ClassId = classId, ActorId = actorId, Now = now },
                    tx,
                    cancellationToken: ct))
                .ConfigureAwait(false);
        }
    }

    private async Task InsertComponentsAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid examId,
        IList<ExamMarkComponentEntity> components,
        Guid actorId,
        DateTime now,
        CancellationToken ct)
    {
        foreach (ExamMarkComponentEntity component in components)
        {
            component.Id = component.Id == Guid.Empty ? Guid.NewGuid() : component.Id;
            component.ExamId = examId;
            EnsureInsertAudit(component, now, actorId);

            string sql = $"""
                INSERT INTO {Schema}.{DatabaseConfig.TableExamMarkComponents}
                    (id, examid, name, maxmarks, passingmarks, displayorder,
                     isactive, versionno, createdby, createdon, updatedby, updatedon)
                VALUES
                    (@Id, @ExamId, @Name, @MaxMarks, @PassingMarks, @DisplayOrder,
                     @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn)
                ON CONFLICT (id)
                DO UPDATE SET name = @Name, maxmarks = @MaxMarks, passingmarks = @PassingMarks,
                              displayorder = @DisplayOrder, isactive = true,
                              updatedby = @UpdatedBy, updatedon = @UpdatedOn,
                              versionno = {DatabaseConfig.TableExamMarkComponents}.versionno + 1;
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, component, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
    }

    public async Task SoftDeleteExamAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExams}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableExamClasses}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE examid = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableExamMarkComponents}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE examid = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableExamSchedules}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE examid = @Id;
            """;

        await connection.ExecuteAsync(
                new CommandDefinition(sql, new { Id = id, ActorId = actorId, Now = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task UpdateExamStatusAsync(Guid id, ExamStatus status, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExams}
            SET status = @Status, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id, Status = (short)status, ActorId = ResolveUpdateActor(), Now = SchoolLocalTime.NowDateTime() },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task MarkResultDeclaredAsync(Guid examId, DateTime declaredOn, Guid declaredBy, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExams}
            SET resultdeclared = true,
                resultdeclaredon = @DeclaredOn,
                resultdeclaredby = @DeclaredBy,
                status = @Status,
                updatedby = @DeclaredBy,
                updatedon = @DeclaredOn,
                versionno = versionno + 1
            WHERE id = @ExamId AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { ExamId = examId, DeclaredOn = declaredOn, DeclaredBy = declaredBy, Status = (short)ExamStatus.ResultDeclared },
                cancellationToken: ct))
            .ConfigureAwait(false);
    }

    // ── Schedules ────────────────────────────────────────────

    public async Task<IList<ExamScheduleRow>> GetSchedulesAsync(Guid? examId, Guid? classId, CancellationToken ct = default)
    {
        await _scope.EnsureLoadedAsync(ct).ConfigureAwait(false);
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        var where = new StringBuilder($"sc.isactive = true AND e.isactive = true{YearFilter("e")}");
        var parameters = new DynamicParameters();
        parameters.Add("ScopeAcademicYearId", _scope.ActiveAcademicYearId);
        await BranchSqlBuilder.AppendActiveBranchFilterAsync(_branchContext, where, parameters, "e", ct)
            .ConfigureAwait(false);

        if (examId.HasValue && examId.Value != Guid.Empty)
        {
            where.Append(" AND sc.examid = @ExamId");
            parameters.Add("ExamId", examId.Value);
        }

        if (classId.HasValue && classId.Value != Guid.Empty)
        {
            where.Append(" AND sc.classid = @ClassId");
            parameters.Add("ClassId", classId.Value);
        }

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count == 0)
            {
                return [];
            }

            // Section-level access is enough; do not also require exam-group mapping
            // (stale/missing mappings were hiding Class 1 schedules for teachers who had rights).
            where.Append(" AND sc.classid = ANY(@ScopeClassIds)");
            parameters.Add("ScopeClassIds", _scope.AllowedClassIds.ToArray());
        }

        string sql = $"""
            SELECT sc.id AS Id,
                   sc.examid AS ExamId,
                   COALESCE(e.name, '') AS ExamName,
                   sc.classid AS ClassId,
                   COALESCE({DashboardClassLabel.DisplayNameSql}, '') AS ClassName,
                   sc.subjectid AS SubjectId,
                   COALESCE(s.subjectname, '') AS SubjectName,
                   sc.examdate AS ExamDate,
                   sc.starttime AS StartTime,
                   sc.endtime AS EndTime,
                   sc.roomno AS RoomNo,
                   sc.invigilatorid AS InvigilatorId,
                   CASE WHEN emp.id IS NULL THEN NULL
                        ELSE TRIM(COALESCE(empu.firstname, '') || ' ' || COALESCE(empu.lastname, ''))
                   END AS InvigilatorName,
                   COALESCE((SELECT SUM(mc.maxmarks) FROM {Schema}.{DatabaseConfig.TableExamMarkComponents} mc
                             WHERE mc.examid = sc.examid AND mc.isactive = true), 0) AS MaxMarks
            FROM {Schema}.{DatabaseConfig.TableExamSchedules} sc
            INNER JOIN {Schema}.{DatabaseConfig.TableExams} e ON e.id = sc.examid
            INNER JOIN {Schema}.{DatabaseConfig.TableClasses} c ON c.id = sc.classid
            INNER JOIN {Schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            INNER JOIN {Schema}.{DatabaseConfig.TableSubjects} s
                ON s.id = sc.subjectid
               AND s.isactive = true
               AND s.classgroupid = c.classgroupid
            LEFT JOIN {Schema}.{DatabaseConfig.TableEmployees} emp ON emp.id = sc.invigilatorid
            LEFT JOIN {IdentitySchema}.{DatabaseConfig.TableUsers} empu ON empu.id = emp.userid
            WHERE {where}
            ORDER BY sc.examdate, sc.starttime NULLS LAST;
            """;

        IEnumerable<ExamScheduleRow> rows = await connection.QueryAsync<ExamScheduleRow>(
                new CommandDefinition(sql, parameters, cancellationToken: ct))
            .ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ExamScheduleEntity?> GetScheduleByIdAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT id, examid, classid, subjectid, examdate, starttime, endtime, roomno, invigilatorid,
                   isactive, versionno, createdby, createdon, updatedby, updatedon
            FROM {Schema}.{DatabaseConfig.TableExamSchedules}
            WHERE id = @Id AND isactive = true;
            """;

        return await connection.QuerySingleOrDefaultAsync<ExamScheduleEntity>(
                new CommandDefinition(sql, new { Id = id }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateScheduleAsync(ExamScheduleEntity schedule, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        schedule.Id = schedule.Id == Guid.Empty ? Guid.NewGuid() : schedule.Id;
        EnsureInsertAudit(schedule, now, actorId);

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableExamSchedules}
                (id, examid, classid, subjectid, examdate, starttime, endtime, roomno, invigilatorid,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ExamId, @ClassId, @SubjectId, @ExamDate, @StartTime, @EndTime, @RoomNo, @InvigilatorId,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, schedule, cancellationToken: ct)).ConfigureAwait(false);
        return schedule.Id;
    }

    public async Task CreateSchedulesAsync(IList<ExamScheduleEntity> schedules, CancellationToken ct = default)
    {
        if (schedules.Count == 0)
        {
            return;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveInsertActor();

        foreach (ExamScheduleEntity schedule in schedules)
        {
            schedule.Id = schedule.Id == Guid.Empty ? Guid.NewGuid() : schedule.Id;
            EnsureInsertAudit(schedule, now, actorId);
        }

        string sql = $"""
            INSERT INTO {Schema}.{DatabaseConfig.TableExamSchedules}
                (id, examid, classid, subjectid, examdate, starttime, endtime, roomno, invigilatorid,
                 isactive, versionno, createdby, createdon, updatedby, updatedon)
            VALUES
                (@Id, @ExamId, @ClassId, @SubjectId, @ExamDate, @StartTime, @EndTime, @RoomNo, @InvigilatorId,
                 @IsActive, @VersionNo, @CreatedBy, @CreatedOn, @UpdatedBy, @UpdatedOn);
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, schedules, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task UpdateScheduleAsync(ExamScheduleEntity schedule, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        ApplyUpdateAudit(schedule, ResolveUpdateActor(), SchoolLocalTime.NowDateTime());

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamSchedules}
            SET classid = @ClassId,
                subjectid = @SubjectId,
                examdate = @ExamDate,
                starttime = @StartTime,
                endtime = @EndTime,
                roomno = @RoomNo,
                invigilatorid = @InvigilatorId,
                updatedby = @UpdatedBy,
                updatedon = @UpdatedOn,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, schedule, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SoftDeleteScheduleAsync(Guid id, CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);
        DateTime now = SchoolLocalTime.NowDateTime();
        Guid actorId = ResolveUpdateActor();

        string sql = $"""
            UPDATE {Schema}.{DatabaseConfig.TableExamSchedules}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE id = @Id;
            UPDATE {Schema}.{DatabaseConfig.TableExamStudentMarks}
            SET isactive = false, updatedby = @ActorId, updatedon = @Now, versionno = versionno + 1
            WHERE examscheduleid = @Id;
            """;

        await connection.ExecuteAsync(
                new CommandDefinition(sql, new { Id = id, ActorId = actorId, Now = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    public async Task<bool> ScheduleExistsAsync(
        Guid examId,
        Guid classId,
        Guid subjectId,
        Guid? excludeId,
        CancellationToken ct = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(ct).ConfigureAwait(false);

        string sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM {Schema}.{DatabaseConfig.TableExamSchedules}
                WHERE examid = @ExamId AND classid = @ClassId AND subjectid = @SubjectId
                  AND isactive = true
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            );
            """;

        return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    sql,
                    new { ExamId = examId, ClassId = classId, SubjectId = subjectId, ExcludeId = excludeId },
                    cancellationToken: ct))
            .ConfigureAwait(false);
    }
}
