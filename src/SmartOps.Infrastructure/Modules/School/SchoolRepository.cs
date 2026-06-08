using System.Data;
using Dapper;
using Microsoft.Extensions.Options;

using Npgsql;

using SmartOps.Application.Abstractions;

using SmartOps.Application.Configuration;

using SmartOps.Application.Modules.School;

using SmartOps.Application.Modules.School.Interfaces;

using SmartOps.Domain.Common.Enums;

using SmartOps.Domain.Common.Models;

using SmartOps.Domain.Modules.School.Entities;

using SmartOps.Domain.Modules.School;

using SmartOps.Infrastructure.Persistence.Context;

using SmartOps.Infrastructure.Persistence;

using SmartOps.Domain.Common.Configuration;



namespace SmartOps.Infrastructure.Modules.School;



public sealed class SchoolRepository : BaseRepository, ISchoolRepository

{

    private const string BranchesTable = DatabaseConfig.TableSchoolBranches;



    private readonly IDbConnectionFactory _connectionFactory;

    private readonly ITenantProvisioningService _tenantProvisioning;

    private readonly ISchoolDatabaseProvisioner _schoolDatabaseProvisioner;

    private readonly ISchoolSettingsRepository _schoolSettings;

    private readonly ISchoolDefaultAdminProvisioner _defaultAdminProvisioner;

    private readonly PerSchoolDatabaseOptions _perSchoolDbOptions;



    public SchoolRepository(

        DapperContext context,

        ICurrentUserService currentUser,

        IDbConnectionFactory connectionFactory,

        ITenantProvisioningService tenantProvisioning,

        ISchoolDatabaseProvisioner schoolDatabaseProvisioner,

        ISchoolSettingsRepository schoolSettings,

        ISchoolDefaultAdminProvisioner defaultAdminProvisioner,

        IOptions<PerSchoolDatabaseOptions> perSchoolDbOptions)

        : base(context, currentUser)

    {

        _connectionFactory = connectionFactory;

        _tenantProvisioning = tenantProvisioning;

        _schoolDatabaseProvisioner = schoolDatabaseProvisioner;

        _schoolSettings = schoolSettings;

        _defaultAdminProvisioner = defaultAdminProvisioner;

        _perSchoolDbOptions = perSchoolDbOptions.Value;

    }



    public async Task<Guid> CreateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default)

    {

        var utcNow = DateTime.UtcNow;

        if (school.Id == Guid.Empty)

        {

            school.Id = Guid.NewGuid();

        }



        EnsureInsertAudit(school, utcNow);



        await using NpgsqlConnection connection = await OpenPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);



        Guid schoolId = await WithTransactionAsync(connection, async (conn, tx) =>

        {

            await InsertAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableSchools, school, tx)

                .ConfigureAwait(false);



            foreach (var branch in school.Branches)

            {

                branch.Id = branch.Id == Guid.Empty ? Guid.NewGuid() : branch.Id;

                branch.SchoolId = school.Id;

                EnsureInsertAudit(branch, utcNow);

                await InsertAsync(conn, DatabaseConfig.Schema_Global, BranchesTable, branch, tx).ConfigureAwait(false);

            }



            return school.Id;

        }).ConfigureAwait(false);



        if (_perSchoolDbOptions.Enabled)

        {

            (string databaseName, string connectionString) = await _schoolDatabaseProvisioner

                .ProvisionAsync(schoolId, school.Subdomain, cancellationToken)

                .ConfigureAwait(false);



            school.DatabaseName = databaseName;

            school.ConnectionString = connectionString;

            await UpdateSchoolConnectionAsync(school, cancellationToken).ConfigureAwait(false);

        }

        else if (!string.IsNullOrWhiteSpace(school.SchemaName))

        {

            await _tenantProvisioning

                .ProvisionSchemaAsync(school.SchemaName, cancellationToken)

                .ConfigureAwait(false);

        }



        if (!_perSchoolDbOptions.Enabled)

        {

            await _schoolSettings.SeedLeaveDefaultsAsync(schoolId, cancellationToken).ConfigureAwait(false);

        }



        await _defaultAdminProvisioner.ProvisionAsync(school, cancellationToken).ConfigureAwait(false);



        return schoolId;

    }



    public async Task<SchoolEntity?> GetSchoolBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(subdomain))

        {

            return null;

        }



        IDbConnection connection = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $@"

            SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}

            WHERE subdomain = @Subdomain AND isactive = true

            LIMIT 1";



        return await connection

            .QuerySingleOrDefaultAsync<SchoolEntity>(sql, new { Subdomain = subdomain.Trim().ToLowerInvariant() })

            .ConfigureAwait(false);

    }



    public async Task<SchoolEntity?> GetSchoolByIdAsync(Guid id, CancellationToken cancellationToken = default)

    {

        IDbConnection connection = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"SELECT * FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools} WHERE id = @Id AND isactive = true";

        var school = await connection.QuerySingleOrDefaultAsync<SchoolEntity>(sql, new { Id = id }).ConfigureAwait(false);



        if (school is null)

        {

            return null;

        }



        var branchSql = $"SELECT * FROM {DatabaseConfig.Schema_Global}.{BranchesTable} WHERE schoolid = @SchoolId AND isactive = true ORDER BY isheadoffice DESC, name ASC";

        school.Branches = (await connection.QueryAsync<SchoolBranchEntity>(branchSql, new { SchoolId = id }).ConfigureAwait(false)).ToList();

        return school;

    }



    public async Task<PagedResult<SchoolListModel>> GetAllSchoolsAsync(

        int pageIndex,

        int pageSize,

        string? searchTerm = null,

        string? sortColumn = null,

        string? sortDirection = null,

        SchoolFilter filter = SchoolFilter.Active,

        CancellationToken cancellationToken = default)

    {

        IDbConnection connection = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);



        var whereClause = "WHERE 1 = 1";



        switch (filter)

        {

            case SchoolFilter.Active:

                whereClause += " AND isactive = true";

                break;

            case SchoolFilter.Inactive:

                whereClause += " AND isactive = false";

                break;

        }



        if (!string.IsNullOrWhiteSpace(searchTerm))

        {

            whereClause += " AND (name ILIKE @SearchTerm OR schoolcode ILIKE @SearchTerm OR subdomain ILIKE @SearchTerm OR primaryemail ILIKE @SearchTerm OR city ILIKE @SearchTerm)";

            searchTerm = $"%{searchTerm}%";

        }



        var direction = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        var orderBy = sortColumn?.ToLowerInvariant() switch

        {

            "schoolcode" => $"schoolcode {direction}",

            "city" => $"city {direction}",

            "subdomain" => $"subdomain {direction}",

            _ => $"name {direction}"

        };



        var countSql = $"SELECT COUNT(*) FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools} {whereClause}";

        var querySql = $@"

            SELECT

                id,

                name,

                schoolcode,

                primaryemail,

                city,

                state,

                affiliatedboard,

                subdomain,

                isactive

            FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}

            {whereClause}

            ORDER BY {orderBy}";



        return await GetPagedResultAsync<SchoolListModel>(

            connection,

            querySql,

            countSql,

            new { SearchTerm = searchTerm },

            pageIndex,

            pageSize).ConfigureAwait(false);

    }



    public async Task UpdateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default)

    {

        var utcNow = DateTime.UtcNow;

        var actorId = ResolveUpdateActor();

        ApplyUpdateAudit(school, actorId, utcNow);



        await using NpgsqlConnection connection = await OpenPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);



        await WithTransactionAsync(connection, async (conn, tx) =>

        {

            await UpdateAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableSchools, school, tx, "Id")

                .ConfigureAwait(false);



            var deactivateSql = $@"

                UPDATE {DatabaseConfig.Schema_Global}.{BranchesTable}

                SET isactive = false, updatedon = @UpdatedOn, updatedby = @UpdatedBy

                WHERE schoolid = @SchoolId";

            await conn.ExecuteAsync(deactivateSql, new

            {

                school.Id,

                UpdatedOn = utcNow,

                UpdatedBy = actorId

            }, tx).ConfigureAwait(false);



            foreach (var branch in school.Branches)

            {

                branch.SchoolId = school.Id;

                if (branch.Id == Guid.Empty)

                {

                    branch.Id = Guid.NewGuid();

                    EnsureInsertAudit(branch, utcNow);

                    await InsertAsync(conn, DatabaseConfig.Schema_Global, BranchesTable, branch, tx).ConfigureAwait(false);

                }

                else

                {

                    branch.IsActive = true;

                    ApplyUpdateAudit(branch, actorId, utcNow);

                    await UpdateAsync(conn, DatabaseConfig.Schema_Global, BranchesTable, branch, tx, "Id").ConfigureAwait(false);

                }

            }

        }).ConfigureAwait(false);

    }



    public async Task UpdateSchoolConnectionAsync(SchoolEntity school, CancellationToken cancellationToken = default)

    {

        IDbConnection connection = await Context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);

        const string sql = $"""

UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}

SET databasename = @DatabaseName,

    connectionstring = @ConnectionString,

    updatedon = @UpdatedOn,

    updatedby = @UpdatedBy,

    versionno = versionno + 1

WHERE id = @Id;

""";

        await connection.ExecuteAsync(

            new CommandDefinition(

                sql,

                new

                {

                    school.Id,

                    school.DatabaseName,

                    school.ConnectionString,

                    UpdatedOn = DateTime.UtcNow,

                    UpdatedBy = ResolveUpdateActor(Guid.Parse(DatabaseConfig.SystemUserId))

                },

                cancellationToken: cancellationToken)).ConfigureAwait(false);

    }



    public async Task DeleteSchoolAsync(Guid id, CancellationToken cancellationToken = default)

    {

        await using NpgsqlConnection connection = await OpenPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);



        await WithTransactionAsync(connection, async (conn, tx) =>

        {

            await SoftDeleteAsync(conn, DatabaseConfig.Schema_Global, DatabaseConfig.TableSchools, id, tx)

                .ConfigureAwait(false);

        }).ConfigureAwait(false);

    }



    private async Task<NpgsqlConnection> OpenPlatformConnectionAsync(CancellationToken cancellationToken) =>

        (NpgsqlConnection)await _connectionFactory.CreatePlatformConnectionAsync(cancellationToken).ConfigureAwait(false);

}

