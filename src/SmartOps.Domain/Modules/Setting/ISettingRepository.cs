using SmartOps.Domain.Modules.Setting.Entities;

namespace SmartOps.Domain.Modules.Setting;

public interface ISettingRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task UpdateValueAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<int> GetNextSequenceAsync(string key, CancellationToken cancellationToken = default);
}
