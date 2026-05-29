using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Modules.Setting;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Setting;

public sealed class SettingRepository : BaseRepository, ISettingRepository
{
    public SettingRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
SELECT value FROM {Context.OperationalSchema}.{DatabaseConfig.TableSettings}
WHERE key = @Key AND isactive = true
""";
        return await connection.QuerySingleOrDefaultAsync<string>(sql, new { Key = key }).ConfigureAwait(false);
    }

    public async Task UpdateValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"""
UPDATE {Context.OperationalSchema}.{DatabaseConfig.TableSettings}
SET value = @Value, updatedon = NOW()
WHERE key = @Key
""";
        await connection.ExecuteAsync(sql, new { Key = key, Value = value }).ConfigureAwait(false);
    }

}
