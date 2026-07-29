using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Identity;

public sealed class UserTypeRepository : BaseRepository, IUserTypeRepository
{
    public UserTypeRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IReadOnlyList<UserTypeEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id AS Id, name AS Name, isactive AS IsActive,
       versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {CatalogSchema}.{DatabaseConfig.TableUserTypes}
WHERE isactive = true
ORDER BY
  CASE lower(trim(name))
    WHEN 'admin' THEN 1
    WHEN 'school admin' THEN 2
    WHEN 'principal' THEN 3
    WHEN 'student' THEN 4
    WHEN 'teacher' THEN 5
    WHEN 'accountant' THEN 6
    WHEN 'non-academic staff' THEN 7
    WHEN 'office staff' THEN 8
    ELSE 99
  END,
  name;
""";
        IEnumerable<UserTypeEntity> rows = await connection.QueryAsync<UserTypeEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Guid? known = UserTypeCodes.TryGetId(name);
        if (known.HasValue)
        {
            return known;
        }

        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id FROM {CatalogSchema}.{DatabaseConfig.TableUserTypes}
WHERE lower(trim(name)) = lower(trim(@Name)) AND isactive = true
LIMIT 1;
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public Task<Guid?> GetIdByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        GetIdByNameAsync(code, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetUserIdsByTypeCodesAsync(
        Guid schoolId,
        IReadOnlyList<string> typeCodes,
        CancellationToken cancellationToken = default)
    {
        if (typeCodes.Count == 0)
        {
            return [];
        }

        _ = schoolId;
        Guid[] typeIds = typeCodes
            .Select(c => UserTypeCodes.TryGetId(c))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (typeIds.Length == 0)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT DISTINCT u.id
FROM {IdentitySchema}.{DatabaseConfig.TableUsers} u
WHERE u.isactive = true AND u.usertypeid = ANY(@TypeIds);
""";
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                sql,
                new { TypeIds = typeIds },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, UserTypeSummary>> GetUserTypesForSchoolUsersAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        _ = schoolId;
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT u.id AS UserId, u.usertypeid AS UserTypeId
FROM {IdentitySchema}.{DatabaseConfig.TableUsers} u
WHERE u.isactive = true;
""";
        IEnumerable<UserTypeMappingRow> rows = await connection.QueryAsync<UserTypeMappingRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.ToDictionary(
            r => r.UserId,
            r =>
            {
                string name = UserTypeCodes.GetName(r.UserTypeId) ?? string.Empty;
                return new UserTypeSummary { UserTypeId = r.UserTypeId, Code = name, Name = name };
            });
    }

    private sealed class UserTypeMappingRow
    {
        public Guid UserId { get; set; }

        public Guid UserTypeId { get; set; }
    }
}
