using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Configuration;
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

    private static string G => DatabaseConfig.Schema_Global;

    public async Task<IReadOnlyList<UserTypeEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id AS Id, code AS Code, name AS Name, isactive AS IsActive,
       versionno AS VersionNo, createdby AS CreatedBy, createdon AS CreatedOn,
       updatedby AS UpdatedBy, updatedon AS UpdatedOn
FROM {G}.{DatabaseConfig.TableUserTypes}
WHERE isactive = true
ORDER BY name;
""";
        IEnumerable<UserTypeEntity> rows = await connection.QueryAsync<UserTypeEntity>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<Guid?> GetIdByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT id FROM {G}.{DatabaseConfig.TableUserTypes}
WHERE code = @Code AND isactive = true
LIMIT 1;
""";
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { Code = code.Trim() }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsByTypeCodesAsync(
        Guid schoolId,
        IReadOnlyList<string> typeCodes,
        CancellationToken cancellationToken = default)
    {
        if (typeCodes.Count == 0)
        {
            return [];
        }

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT DISTINCT u.id
FROM {G}.{DatabaseConfig.TableUsers} u
INNER JOIN {G}.{DatabaseConfig.TableUserSchoolMappings} m
    ON m.userid = u.id AND m.schoolid = @SchoolId AND m.isactive = true
INNER JOIN {G}.{DatabaseConfig.TableUserTypes} ut
    ON ut.id = m.usertypeid AND ut.isactive = true
WHERE u.isactive = true AND ut.code = ANY(@TypeCodes);
""";
        IEnumerable<Guid> rows = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                sql,
                new { SchoolId = schoolId, TypeCodes = typeCodes.ToArray() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, UserTypeSummary>> GetUserTypesForSchoolUsersAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string sql = $"""
SELECT m.userid AS UserId, ut.id AS UserTypeId, ut.code AS Code, ut.name AS Name
FROM {G}.{DatabaseConfig.TableUserSchoolMappings} m
INNER JOIN {G}.{DatabaseConfig.TableUserTypes} ut ON ut.id = m.usertypeid AND ut.isactive = true
WHERE m.schoolid = @SchoolId AND m.isactive = true AND m.usertypeid IS NOT NULL;
""";
        IEnumerable<UserTypeMappingRow> rows = await connection.QueryAsync<UserTypeMappingRow>(
            new CommandDefinition(sql, new { SchoolId = schoolId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.ToDictionary(
            r => r.UserId,
            r => new UserTypeSummary { UserTypeId = r.UserTypeId, Code = r.Code, Name = r.Name });
    }

    private sealed class UserTypeMappingRow
    {
        public Guid UserId { get; set; }

        public Guid UserTypeId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
