namespace SmartOps.Application.Modules.School.Interfaces;

public interface ISchoolSettingsRepository
{
    Task<IReadOnlyList<SchoolSettingRow>> GetByPrefixAsync(
        Guid schoolId,
        string keyPrefix,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        Guid schoolId,
        IReadOnlyList<SchoolSettingUpsert> settings,
        CancellationToken cancellationToken = default);

    Task SeedLeaveDefaultsAsync(Guid schoolId, CancellationToken cancellationToken = default);
}

public sealed class SchoolSettingRow
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class SchoolSettingUpsert
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
