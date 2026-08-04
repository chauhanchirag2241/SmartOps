using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
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
        if (classEntity.ClassGroupId == Guid.Empty)
        {
            throw new InvalidOperationException("Class group is required.");
        }

        if (string.IsNullOrWhiteSpace(classEntity.Section))
        {
            throw new InvalidOperationException("Section is required.");
        }

        classEntity.Section = classEntity.Section.Trim();

        var now = SchoolLocalTime.NowDateTime();
        if (classEntity.Id == Guid.Empty)
        {
            classEntity.Id = Guid.NewGuid();
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupExists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    $@"SELECT 1 FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE id = @ClassGroupId AND isactive = true;",
                    new { classEntity.ClassGroupId },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!groupExists.HasValue)
            {
                throw new InvalidOperationException("Class group was not found.");
            }

            var existingSql = $@"
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                WHERE classgroupid = @ClassGroupId
                AND lower(section) = lower(@Section)
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
                    "A class with the same section already exists for this class group.");
            }

            EnsureInsertAudit(classEntity, now);
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
                cg.branchid AS BranchId,
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
        Guid? classGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var whereClause = BuildListWhereClause(filter, ref searchTerm);
        if (classGroupId.HasValue && classGroupId.Value != Guid.Empty)
        {
            whereClause += " AND c.classgroupid = @ClassGroupId";
        }

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
                c.section AS Section,
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
                    ClassGroupId = classGroupId,
                    ScopeClassIds = _scope.AllowedClassIds.ToArray(),
                    ActiveBranchId = _branchContext.ActiveBranchId
                },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ClassGroupListModel>> GetAllClassGroupsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        bool scopeToActiveBranch = false,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (scopeToActiveBranch)
        {
            await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);
        }

        var whereClause = BuildGroupListWhereClause(filter, ref searchTerm);
        if (scopeToActiveBranch)
        {
            whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "cg", ref whereClause);
        }

        var orderBy = ResolveGroupListOrderBy(sortColumn, sortDirection);
        var schema = Context.OperationalSchema;
        var man = DatabaseConfig.Schema_Man;

        var countSql = $@"
            SELECT COUNT(*)
            FROM {schema}.{DatabaseConfig.TableClassGroups} cg
            LEFT JOIN {man}.{DatabaseConfig.TableSchoolBranches} b ON b.id = cg.branchid
            {whereClause};";

        var querySql = $@"
            SELECT
                cg.id AS Id,
                cg.branchid AS BranchId,
                COALESCE(b.name, 'N/A') AS BranchName,
                cg.classname AS ClassName,
                cg.description AS Description,
                (
                    SELECT COUNT(1)::int
                    FROM {schema}.{DatabaseConfig.TableClasses} c
                    WHERE c.classgroupid = cg.id AND c.isactive = true
                ) AS SectionCount,
                (
                    SELECT COUNT(1)::int
                    FROM {schema}.{DatabaseConfig.TableSubjects} s
                    WHERE s.classgroupid = cg.id AND s.isactive = true
                ) AS SubjectCount,
                CASE WHEN cg.isactive THEN 'Active' ELSE 'Inactive' END AS Status,
                cg.isactive AS IsActive
            FROM {schema}.{DatabaseConfig.TableClassGroups} cg
            LEFT JOIN {man}.{DatabaseConfig.TableSchoolBranches} b ON b.id = cg.branchid
            {whereClause}
            ORDER BY {orderBy}";

        return await GetPagedResultAsync<ClassGroupListModel>(
                connection,
                querySql,
                countSql,
                new
                {
                    SearchTerm = searchTerm,
                    ActiveBranchId = _branchContext.ActiveBranchId
                },
                pageIndex,
                pageSize)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateClassGroupAsync(ClassGroupEntity group, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group.ClassName))
        {
            throw new InvalidOperationException("Class name is required.");
        }

        group.ClassName = group.ClassName.Trim();
        group.Description = string.IsNullOrWhiteSpace(group.Description) ? null : group.Description.Trim();

        var now = SchoolLocalTime.NowDateTime();
        if (group.Id == Guid.Empty)
        {
            group.Id = Guid.NewGuid();
        }

        group.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(group.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var exists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    $@"SELECT 1 FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE branchid = @BranchId
                         AND lower(classname) = lower(@ClassName)
                         AND isactive = true;",
                    new { group.BranchId, group.ClassName },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (exists.HasValue)
            {
                throw new InvalidOperationException("A class group with the same name already exists for this branch.");
            }

            // Reactivate inactive match if present.
            var inactiveId = await conn.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    $@"SELECT id FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE branchid = @BranchId
                         AND lower(classname) = lower(@ClassName)
                         AND isactive = false
                       LIMIT 1;",
                    new { group.BranchId, group.ClassName },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (inactiveId.HasValue)
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        $"""
                        UPDATE {schema}.{DatabaseConfig.TableClassGroups}
                        SET isactive = true,
                            description = @Description,
                            updatedby = @ActorId,
                            updatedon = @Now,
                            versionno = versionno + 1
                        WHERE id = @Id;
                        """,
                        new
                        {
                            Id = inactiveId.Value,
                            group.Description,
                            ActorId = ResolveUpdateActor(),
                            Now = now
                        },
                        tx,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                return inactiveId.Value;
            }

            EnsureInsertAudit(group, now);
            return await InsertAsync(conn, schema, DatabaseConfig.TableClassGroups, group, tx)
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateClassGroupAsync(ClassGroupEntity group, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(group.ClassName))
        {
            throw new InvalidOperationException("Class name is required.");
        }

        group.ClassName = group.ClassName.Trim();
        group.Description = string.IsNullOrWhiteSpace(group.Description) ? null : group.Description.Trim();

        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(group, actorId, now);

        group.BranchId = await _branchWrite
            .ResolveWriteBranchIdAsync(group.BranchId, cancellationToken)
            .ConfigureAwait(false);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var exists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    $@"SELECT 1 FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE branchid = @BranchId
                         AND lower(classname) = lower(@ClassName)
                         AND id != @Id
                         AND isactive = true;",
                    new { group.BranchId, group.ClassName, group.Id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (exists.HasValue)
            {
                throw new InvalidOperationException("Another class group with the same name already exists for this branch.");
            }

            await UpdateAsync(conn, schema, DatabaseConfig.TableClassGroups, group, tx, "Id")
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteClassGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var activeSections = await connection.ExecuteScalarAsync<int>(
            $@"SELECT COUNT(1) FROM {schema}.{DatabaseConfig.TableClasses}
               WHERE classgroupid = @Id AND isactive = true;",
            new { Id = id }).ConfigureAwait(false);

        if (activeSections > 0)
        {
            throw new InvalidOperationException("Cannot delete class group while active sections exist.");
        }

        await SoftDeleteAsync(connection, schema, DatabaseConfig.TableClassGroups, id)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassGroupSubjectListModel>> GetClassGroupSubjectsAsync(
        Guid classGroupId,
        CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var sql = $@"
            SELECT
                s.id AS Id,
                s.classgroupid AS ClassGroupId,
                s.id AS SubjectId,
                s.subjectname AS SubjectName,
                s.subjectcode AS SubjectCode,
                CASE WHEN s.isactive THEN 'Active' ELSE 'Inactive' END AS Status,
                s.isactive AS IsActive
            FROM {schema}.{DatabaseConfig.TableSubjects} s
            WHERE s.classgroupid = @ClassGroupId AND s.isactive = true
            ORDER BY s.subjectname ASC;";

        var items = await connection.QueryAsync<ClassGroupSubjectListModel>(
            sql, new { ClassGroupId = classGroupId }).ConfigureAwait(false);
        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> AddClassGroupSubjectAsync(
        Guid classGroupId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        return await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupExists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    $@"SELECT 1 FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE id = @ClassGroupId AND isactive = true;",
                    new { ClassGroupId = classGroupId },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!groupExists.HasValue)
            {
                throw new InvalidOperationException("Class group was not found.");
            }

            var subjectRow = await conn.QuerySingleOrDefaultAsync<dynamic>(
                new CommandDefinition(
                    $@"SELECT id AS Id, classgroupid AS ClassGroupId
                       FROM {schema}.{DatabaseConfig.TableSubjects}
                       WHERE id = @SubjectId AND isactive = true;",
                    new { SubjectId = subjectId },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (subjectRow is null)
            {
                throw new InvalidOperationException("Subject was not found.");
            }

            Guid? existingGroupId = subjectRow.ClassGroupId as Guid?;
            if (existingGroupId == classGroupId)
            {
                throw new InvalidOperationException("This subject is already assigned to the class.");
            }

            if (existingGroupId.HasValue)
            {
                throw new InvalidOperationException("This subject is already assigned to another class.");
            }

            await conn.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    UPDATE {schema}.{DatabaseConfig.TableSubjects}
                    SET classgroupid = @ClassGroupId,
                        updatedby = @ActorId,
                        updatedon = @Now,
                        versionno = versionno + 1
                    WHERE id = @SubjectId AND isactive = true;
                    """,
                    new
                    {
                        ClassGroupId = classGroupId,
                        SubjectId = subjectId,
                        ActorId = actorId,
                        Now = now
                    },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            return subjectId;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveClassGroupSubjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        var updated = await connection.ExecuteAsync(
            $"""
            UPDATE {schema}.{DatabaseConfig.TableSubjects}
            SET classgroupid = NULL,
                updatedby = @ActorId,
                updatedon = @Now,
                versionno = versionno + 1
            WHERE id = @Id AND isactive = true AND classgroupid IS NOT NULL;
            """,
            new { Id = id, ActorId = actorId, Now = now }).ConfigureAwait(false);

        if (updated == 0)
        {
            throw new InvalidOperationException("Subject assignment was not found.");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
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
                {DashboardClassLabel.DisplayNameSql} AS Name,
                c.classgroupid AS ClassGroupId
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            {whereClause}
            ORDER BY cg.classname ASC, c.section ASC;";

        var items = await connection.QueryAsync<DropdownDto>(sql, parameters).ConfigureAwait(false);
        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetTeachingSubjectsForClassAsync(
        Guid classId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (classId == Guid.Empty)
        {
            return [];
        }

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _branchContext.EnsureResolvedAsync(cancellationToken).ConfigureAwait(false);

        var schema = Context.OperationalSchema;

        if (_scope.ScopesEnabled && !_scope.IsGlobalScope)
        {
            if (_scope.AllowedClassIds.Count == 0 || !_scope.AllowedClassIds.Contains(classId))
            {
                return [];
            }

            // Teachers: only subjects mapped to them on this section (CST).
            if (_scope.ScopeType == DataScopeType.Class)
            {
                var teacherSql = $@"
                    SELECT DISTINCT
                        s.id AS Id,
                        s.subjectname AS Name,
                        s.classgroupid AS ClassGroupId
                    FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
                    INNER JOIN {schema}.{DatabaseConfig.TableEmployees} e ON e.id = m.employeeid
                    INNER JOIN {schema}.{DatabaseConfig.TableClasses} c
                        ON c.classgroupid = m.classgroupid AND c.id = @ClassId AND c.isactive = true
                    INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
                    INNER JOIN {schema}.{DatabaseConfig.TableSubjects} s ON s.id = m.subjectid
                    WHERE m.isactive = true
                      AND e.isactive = true
                      AND s.isactive = true
                      AND cg.isactive = true
                      AND e.userid = @UserId
                      AND (@AcademicYearId IS NULL OR m.academicyearid = @AcademicYearId)
                    ORDER BY s.subjectname ASC;";

                var teacherItems = await connection.QueryAsync<DropdownDto>(
                    teacherSql,
                    new
                    {
                        ClassId = classId,
                        UserId = CurrentUser.UserId,
                        AcademicYearId = academicYearId ?? _scope.ActiveAcademicYearId,
                    }).ConfigureAwait(false);
                return teacherItems.ToList();
            }
        }

        string whereClause = """
            WHERE c.id = @ClassId
              AND c.isactive = true
              AND cg.isactive = true
              AND s.isactive = true
            """;
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "cg", ref whereClause);
        whereClause = BranchSqlBuilder.AppendActiveBranchFilter(_branchContext, "s", ref whereClause);

        object parameters = new { ClassId = classId, ActiveBranchId = _branchContext.ActiveBranchId };

        if (_scope.ScopesEnabled
            && !_scope.IsGlobalScope
            && _scope.AllowedSubjectIds.Count > 0)
        {
            whereClause += " AND s.id = ANY(@ScopeSubjectIds)";
            parameters = new
            {
                ClassId = classId,
                ActiveBranchId = _branchContext.ActiveBranchId,
                ScopeSubjectIds = _scope.AllowedSubjectIds.ToArray(),
            };
        }

        var globalSql = $@"
            SELECT DISTINCT
                s.id AS Id,
                s.subjectname AS Name,
                s.classgroupid AS ClassGroupId
            FROM {schema}.{DatabaseConfig.TableClasses} c
            INNER JOIN {schema}.{DatabaseConfig.TableClassGroups} cg ON cg.id = c.classgroupid
            INNER JOIN {schema}.{DatabaseConfig.TableSubjects} s ON s.classgroupid = c.classgroupid
            {whereClause}
            ORDER BY s.subjectname ASC;";

        var globalItems = await connection.QueryAsync<DropdownDto>(globalSql, parameters).ConfigureAwait(false);
        return globalItems.ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DropdownDto>> GetClassGroupDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
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
        if (classEntity.ClassGroupId == Guid.Empty)
        {
            throw new InvalidOperationException("Class group is required.");
        }

        if (string.IsNullOrWhiteSpace(classEntity.Section))
        {
            throw new InvalidOperationException("Section is required.");
        }

        classEntity.Section = classEntity.Section.Trim();

        var now = SchoolLocalTime.NowDateTime();
        var actorId = ResolveUpdateActor();
        ApplyUpdateAudit(classEntity, actorId, now);

        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;

        await WithTransactionAsync(connection, async (conn, tx) =>
        {
            var groupExists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    $@"SELECT 1 FROM {schema}.{DatabaseConfig.TableClassGroups}
                       WHERE id = @ClassGroupId AND isactive = true;",
                    new { classEntity.ClassGroupId },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (!groupExists.HasValue)
            {
                throw new InvalidOperationException("Class group was not found.");
            }

            var existingSql = $@"
                SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses}
                WHERE classgroupid = @ClassGroupId
                AND lower(section) = lower(@Section)
                AND id != @Id
                AND isactive = true;";

            var exists = await conn.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    existingSql,
                    new { classEntity.ClassGroupId, classEntity.Section, classEntity.Id },
                    tx,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (exists.HasValue)
            {
                throw new InvalidOperationException(
                    "Another class with the same section already exists for this class group.");
            }

            await UpdateAsync(conn, schema, DatabaseConfig.TableClasses, classEntity, tx, "Id")
                .ConfigureAwait(false);
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
            $"""
SELECT COUNT(1)
FROM {schema}.{DatabaseConfig.TableClassSubjectTeacherMappings} m
INNER JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.classgroupid = m.classgroupid
WHERE c.id = @Id AND m.isactive = true;
""",
            new { Id = id }).ConfigureAwait(false);

        if (teacherMappingCount > 0)
        {
            throw new InvalidOperationException("Cannot delete class as it is already mapped with subject or teacher.");
        }

        await SoftDeleteAsync(connection, schema, DatabaseConfig.TableClasses, id)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RecoverClassAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var schema = Context.OperationalSchema;
        var now = SchoolLocalTime.NowDateTime();
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
            where += " AND (cg.classname ILIKE @SearchTerm OR c.section ILIKE @SearchTerm OR c.roomnumber ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string BuildGroupListWhereClause(ClassFilter filter, ref string? searchTerm)
    {
        var where = "WHERE 1 = 1";

        switch (filter)
        {
            case ClassFilter.Active:
                where += " AND cg.isactive = true";
                break;
            case ClassFilter.Inactive:
                where += " AND cg.isactive = false";
                break;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (cg.classname ILIKE @SearchTerm OR cg.description ILIKE @SearchTerm OR b.name ILIKE @SearchTerm)";
            searchTerm = $"%{searchTerm}%";
        }

        return where;
    }

    private static string ResolveListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        // Default: class name then section ascending (A, B, C) everywhere sections are listed.
        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "cg.classname ASC, c.section ASC, c.id ASC";
        }

        if (IsSortKey(sortColumn, "className"))
        {
            return $"cg.classname {direction}, c.section ASC, c.id ASC";
        }

        if (IsSortKey(sortColumn, "section"))
        {
            return $"c.section {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "capacity"))
        {
            return $"c.capacity {direction}, c.id ASC";
        }

        if (IsSortKey(sortColumn, "roomNumber"))
        {
            return $"c.roomnumber {direction}, c.id ASC";
        }

        return "cg.classname ASC, c.section ASC, c.id ASC";
    }

    private static string ResolveGroupListOrderBy(string? sortColumn, string? sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        if (string.IsNullOrWhiteSpace(sortColumn))
        {
            return "cg.classname ASC, cg.id ASC";
        }

        if (IsSortKey(sortColumn, "className"))
        {
            return $"cg.classname {direction}, cg.id ASC";
        }

        if (IsSortKey(sortColumn, "branchName"))
        {
            return $"b.name {direction}, cg.id ASC";
        }

        return "cg.classname ASC, cg.id ASC";
    }

    private static bool IsSortKey(string sortColumn, params string[] keys)
    {
        return keys.Any(k => string.Equals(sortColumn, k, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
