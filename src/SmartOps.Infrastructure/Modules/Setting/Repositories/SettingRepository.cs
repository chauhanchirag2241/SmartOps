using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Domain.Modules.Setting.Interfaces;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Setting.Repositories;

public sealed class SettingRepository : BaseRepository, ISettingRepository
{
    public SettingRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"SELECT value FROM {DatabaseConfig.Schema_Global}.settings WHERE key = @Key AND isactive = true";
        return await connection.QuerySingleOrDefaultAsync<string>(sql, new { Key = key }).ConfigureAwait(false);
    }

    public async Task UpdateValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sql = $"UPDATE {DatabaseConfig.Schema_Global}.settings SET value = @Value, updatedon = NOW() WHERE key = @Key";
        await connection.ExecuteAsync(sql, new { Key = key, Value = value }).ConfigureAwait(false);
    }

    public async Task<int> GetNextSequenceAsync(string key, CancellationToken cancellationToken = default)
    {
        var connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        
        // Atomic increment and return
        var sql = $@"
            UPDATE {DatabaseConfig.Schema_Global}.settings 
            SET value = (CAST(value AS INTEGER) + 1)::text, 
                updatedon = NOW() 
            WHERE key = @Key 
            RETURNING value";
            
        var nextValue = await connection.QuerySingleAsync<string>(sql, new { Key = key }).ConfigureAwait(false);
        return int.Parse(nextValue);
    }
}
