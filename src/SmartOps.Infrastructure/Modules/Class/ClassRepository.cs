using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Branch;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using System.Data;
using SmartOps.Application.Modules.Authorization;

namespace SmartOps.Infrastructure.Modules.Class;

/// <summary>
/// Class aggregate persistence. Same pattern as <see cref="StudentRepository"/>.
/// </summary>
public sealed class ClassRepository : BaseRepository, IClassRepository
{
    private readonly IUserScopeContext _scope;
    private readonly IBranchContext _branchContext;
    private readonly IBranchScopedWriteHelper _branchWrite;

    public ClassRepository(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserScopeContext scope,
        IBranchContext branchContext,
        IBranchScopedWriteHelper branchWrite)
        : base(context, currentUser)
    {
        _scope = scope;
        _branchContext = branchContext;
        _branchWrite = branchWrite;
    }

    /// <inheritdoc />
    public async Task<Guid> CreateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        if (classEntity.Id == Guid.Empty)
        {
            classEntity.Id = Guid.NewGuid();
        }

        classEntity.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(classEntity.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupId = await FindOrCreateClassGroupAsync(conn, tx, classEntity, utcNow, cancellationToken)
                .ConfigureAwait(false);
            classEntity.ClassGroupId = groupId;

            var existingSql = $@"
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                WHERE classgroupid = @ClassGroupId
                AND section = @Section
                AND isactive = true;";

            var exists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    existingSql,
                    new { classEntity.ClassGroupId, classEntity.Section },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (exists.HasValue)
            {
                throw new InvalidOperationException(
                    "A class with the same name, section and stream/group already exists.");
            }

            EnsureInsertAudit(classEntity, utcNow);
            return await InsertAsync(conn, schema, DatabaseConfig.TableClasses, classEntity, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ClassEntity?> GetClassByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND c.isactive = true";
        var schema = Context.OperationalSchema;

        var sql = $@"
            SELECT
                c.id AS Id,
                c.classgroupid AS ClassGroupId,
                c.section AS Section,
                c.capacity AS Capacity,
                c.roomnumber AS RoomNumber,
                c.shiftid AS ShiftId,
                c.isactive AS IsActive,
                c.createdby AS CreatedBy,
                c.createdon AS CreatedOn,
                c.updatedby AS UpdatedBy,
                c.updatedon AS UpdatedOn,
                c.versionno AS VersionNo,
                cg.classname AS ClassName,
                cg.streamgroup AS StreamGroup,
                cg.branchid AS BranchId,
                cg.medium AS Medium,
                cg.description AS Description
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            WHERE c.id = @Id{activeFilter};";

        return await connection.QuerySingleOrDefaultAsync<ClassEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ClassGroupEntity?> GetClassGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        bool includeInactive = false)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var activeFilter = includeInactive ? string.Empty : " AND isactive = true";
        var schema = Context.OperationalSchema;

        var sql = $@"
            SELECT
                id AS Id,
                branchid AS BranchId,
                classname AS ClassName,
                streamgroup AS StreamGroup,
                medium AS Medium,
                description AS Description,
                isactive AS IsActive,
                createdby AS CreatedBy,
                createdon AS CreatedOn,
                updatedby AS UpdatedBy,
                updatedon AS UpdatedOn,
                versionno AS VersionNo
            FROM {schema}.{DatabaseConfig.TableClassGroups}
            WHERE id = @Id{activeFilter};";

        return await connection.QuerySingleOrDefaultAsync<ClassGroupEntity>(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ClassListModel>> GetAllClassesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "cg", ref whereClause);
        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count > 0)
            {
                whereClause += " AND c.id = ANY(@ScopeClassIds)";
            }
            else
            {
                whereClause += " AND 1 = 0";
            }
        }

        var orderBy = ResolveListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;

        var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            {whereClause};";

        var querySql = $@"
            SELECT
                c.id AS Id,
                c.classgroupid AS ClassGroupId,
                cg.classname AS ClassName,
                CASE c.section
                    WHEN 1 THEN 'A'
                    WHEN 2 THEN 'B'
                    WHEN 3 THEN 'C'
                    WHEN 4 THEN 'D'
                    ELSE 'N/A'
                END AS Section,
                CASE
                    WHEN cg.streamgroup IS NULL THEN NULL
                    WHEN cg.streamgroup = 1 THEN 'None'
                    WHEN cg.streamgroup = 2 THEN 'Science'
                    WHEN cg.streamgroup = 3 THEN 'Commerce'
                    WHEN cg.streamgroup = 4 THEN 'Arts'
                    WHEN cg.streamgroup = 5 THEN 'Regional'
                    WHEN cg.streamgroup = 6 THEN 'Primary'
                    ELSE NULL
                END AS StreamGroup,
                c.capacity AS Capacity,
                COALESCE(c.roomnumber, 'N/A') AS RoomNumber,
                CASE WHEN c.isactive THEN 'Active' ELSE 'Inactive' END AS Status,
                c.isactive AS IsActive
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<ClassListModel>(
                connection,
                querySql,
                countSql,
                new
                {
                    SearchTerm = searchTerm,
                    ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                    ActiveBranchId = _branchContext.ActiveBranchId
                },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        // academicYearId retained for API compatibility; class groups are timeless.
        _ = academicYearId;

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var schema = Context.OperationalSchema;

        string whereClause = "WHERE c.isactive = true AND cg.isactive = true";
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "cg", ref whereClause);

        object parameters = new { ActiveBranchId = _branchContext.ActiveBranchId };

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count > 0)
            {
                whereClause += " AND c.id = ANY(@ScopeClassIds)";
                parameters = new
                {
                    ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                    ActiveBranchId = _branchContext.ActiveBranchId
                };
            }
            else
            {
                return [];
            }
        }

        var sql = $@"
            SELECT
                c.id AS Id,
                {DashboardClassLabel.DisplayNameSql} AS Name
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            {whereClause}
            ORDER BY cg.classname ASC, c.section ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, parameters).ConfigureAwait(false);
        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetClassGroupDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        // academicYearId retained for API compatibility; class groups are timeless.
        _ = academicYearId;

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var schema = Context.OperationalSchema;

        string whereClause = "WHERE cg.isactive = true";
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "cg", ref whereClause);

        object parameters = new { ActiveBranchId = _branchContext.ActiveBranchId };

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count > 0)
            {
                whereClause += $@"
                    AND EXISTS (
                        SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses} c
                        WHERE c.classgroupid = cg.id AND c.isactive = true AND c.id = ANY(@ScopeClassIds))";
                parameters = new
                {
                    ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                    ActiveBranchId = _branchContext.ActiveBranchId
                };
            }
            else
            {
                return [];
            }
        }

        var sql = $@"
            SELECT
                cg.id AS Id,
                cg.classname AS Name
            FROM {schema}.{DatabaseConfig.TableClassGroups} cg
            {whereClause}
            ORDER BY cg.classname ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, parameters).ConfigureAwait(false);
        return items.ToList();
    }

    /// <inheritdoc />
    public async Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(classEntity, actorId, utcNow);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            classEntity.BranchId = await _branchWrite
                .ResolveWriteBranchIdAsync(classEntity.BranchId, cancellationToken)
                .ConfigureAwait(false);

            var groupId = await FindOrCreateClassGroupAsync(conn, tx, classEntity, utcNow, cancellationToken)
                .ConfigureAwait(false);

            var existingSql = $@"
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                WHERE classgroupid = @ClassGroupId
                AND section = @Section
                AND id != @Id
                AND isactive = true;";

            var exists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    existingSql,
                    new { ClassGroupId = groupId, classEntity.Section, classEntity.Id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (exists.HasValue)
            {
                throw new InvalidOperationException(
                    "Another class with the same name, section and stream/group already exists.");
            }

            var previousGroupId = await conn.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    $"SELECT classgroupid FROM {schema}.{DatabaseConfig.TableClasses} WHERE id = @Id;",
                    new { classEntity.Id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            classEntity.ClassGroupId = groupId;
            await UpdateAsync(conn, schema, DatabaseConfig.TableClasses, classEntity, tx, "Id")
                .ConfigureAwait(false);

            // Keep group shared fields in sync when reusing the same group.
            await conn.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableClassGroups}
                    SET medium = @Medium,
                        description = @Description,
                        updatedby = @ActorId,
                        updatedon = @UtcNow,
                        versionno = versionno + 1
                    WHERE id = @ClassGroupId;
                    """,
                    new
                    {
                        ClassGroupId = groupId,
                        classEntity.Medium,
                        classEntity.Description,
                        ActorId = actorId,
                        UtcNow = utcNow
                    },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (previousGroupId.HasValue && previousGroupId.Value != groupId)
            {
                await SoftDeleteGroupIfEmptyAsync(conn, tx, previousGroupId.Value, actorId, utcNow, cancellationToken)
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var studentsCount = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableStudentAcademics} WHERE classid = @Id AND isactive = true;",
            new { Id = id }).ConfigureAwait(false);

        if (studentsCount > 0)
        {
            throw new InvalidOperationException("Cannot delete class as it is already mapped with students.");
        }

        var teacherMappingCount = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} WHERE classid = @Id;",
            new { Id = id }).ConfigureAwait(false);

        if (teacherMappingCount > 0)
        {
            throw new InvalidOperationException("Cannot delete class as it is already mapped with subject or teacher.");
        }

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupId = await conn.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    $"SELECT classgroupid FROM {schema}.{DatabaseConfig.TableClasses} WHERE id = @Id;",
                    new { Id = id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            await SoftDeleteAsync(conn, schema, DatabaseConfig.TableClasses, id, tx)
                .ConfigureAwait(false);

            if (groupId.HasValue)
            {
                await SoftDeleteGroupIfEmptyAsync(
                        conn, tx, groupId.Value, ResolveUpdateActor(), DateTime.UtcNow, cancellationToken)
                    .ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecoverClassAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var now = DateTime.UtcNow;
        var actor = ResolveUpdateActor();

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupId = await conn.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    $"SELECT classgroupid FROM {schema}.{DatabaseConfig.TableClasses} WHERE id = @Id;",
                    new { Id = id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            await conn.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableClasses}
                    SET isactive = true, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
                    WHERE id = @Id AND isactive = false;
                    """,
                    new { Id = id, Now = now, Actor = actor },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (groupId.HasValue)
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        $"""
                        UPDATE {schema}.{DatabaseConfig.TableClassGroups}
                        SET isactive = true, updatedon = @Now, updatedby = @Actor, versionno = versionno + 1
                        WHERE id = @GroupId AND isactive = false;
                        """,
                        new { GroupId = groupId.Value, Now = now, Actor = actor },
                        tx,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    #region Class group helpers

    private async Task<Guid> FindOrCreateClassGroupAsync(
        IDbConnection conn,
        IDbTransaction tx,
        ClassEntity classEntity,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var schema = Context.OperationalSchema;

        var findSql = $@"
            SELECT id FROM {schema}.{DatabaseConfig.TableClassGroups}
            WHERE branchid = @BranchId
              AND classname = @ClassName
              AND streamgroup IS NOT DISTINCT FROM @StreamGroup
              AND isactive = true
            LIMIT 1;";

        var existingId = await conn.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                findSql,
                new
                {
                    classEntity.BranchId,
                    classEntity.ClassName,
                    classEntity.StreamGroup
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        // Reactivate inactive group with same identity if present.
        var inactiveSql = $@"
            SELECT id FROM {schema}.{DatabaseConfig.TableClassGroups}
            WHERE branchid = @BranchId
              AND classname = @ClassName
              AND streamgroup IS NOT DISTINCT FROM @StreamGroup
              AND isactive = false
            LIMIT 1;";

        var inactiveId = await conn.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                inactiveSql,
                new
                {
                    classEntity.BranchId,
                    classEntity.ClassName,
                    classEntity.StreamGroup
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (inactiveId.HasValue)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableClassGroups}
                    SET isactive = true,
                        medium = @Medium,
                        description = @Description,
                        updatedby = @ActorId,
                        updatedon = @UtcNow,
                        versionno = versionno + 1
                    WHERE id = @Id;
                    """,
                    new
                    {
                        Id = inactiveId.Value,
                        classEntity.Medium,
                        classEntity.Description,
                        ActorId = ResolveUpdateActor(),
                        UtcNow = utcNow
                    },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            return inactiveId.Value;
        }

        var group = new ClassGroupEntity
        {
            Id = Guid.NewGuid(),
            BranchId = classEntity.BranchId,
            ClassName = classEntity.ClassName,
            StreamGroup = classEntity.StreamGroup,
            Medium = classEntity.Medium,
            Description = classEntity.Description,
        };
        EnsureInsertAudit(group, utcNow);
        return await InsertAsync(conn, schema, DatabaseConfig.TableClassGroups, group, tx)
            .ConfigureAwait(false);
    }

    private async Task SoftDeleteGroupIfEmptyAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid groupId,
        Guid actorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var schema = Context.OperationalSchema;
        var activeSections = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableClasses}
                   WHERE classgroupid = @GroupId AND isactive = true;",
                new { GroupId = groupId },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (activeSections > 0)
        {
            return;
        }

        await conn.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {schema}.{DatabaseConfig.TableClassGroups}
                SET isactive = false, updatedby = @ActorId, updatedon = @UtcNow, versionno = versionno + 1
                WHERE id = @GroupId AND isactive = true;
                """,
                new { GroupId = groupId, ActorId = actorId, UtcNow = utcNow },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    #endregion

    #region List query helpers

    private static string BuildListWhereClause(ClassFilter filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        switch (filter)
        {
            case ClassFilter.Active:
                where += " AND c.isactive = true";
                break;
            case ClassFilter.Inactive:
                where += " AND c.isactive = false";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (cg.classname ILIKE @SearchTerm OR c.roomnumber ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "c.createdon DESC, c.id ASC";
        }

        if (IsSortKey(sortColumn, "className"))
        {
            return $"cg.classname {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "section"))
        {
            return $"c.section {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "streamGroup"))
        {
            return $"cg.streamgroup {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "academicYear"))
        {
            // Class groups are timeless; fall back to default order.
            return "c.createdon DESC, c.id ASC";
        }

        if (IsSortKey(sortColumn, "capacity"))
        {
            return $"c.capacity {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "roomNumber"))
        {
            return $"c.roomnumber {direction}, c.id ASC";
        }

        return "c.createdon DESC, c.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
