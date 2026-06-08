using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserTypeRepository
{
    Task<IReadOnlyList<UserTypeEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<Guid?> GetIdByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetUserIdsByTypeCodesAsync(
        Guid schoolId,
        IReadOnlyList<string> typeCodes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserTypeSummary>> GetUserTypesForSchoolUsersAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);
}

public sealed class UserTypeSummary
{
    public Guid UserTypeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
