using SmartOps.Domain.Modules.Class.Entities;

namespace SmartOps.Application.Modules.Class.Interfaces;

public interface IClassSettingRepository
{
    Task<ClassSettingEntity?> GetBySectionIdAsync(Guid sectionId, CancellationToken cancellationToken = default);

    Task<Guid?> GetClassTeacherEmployeeIdAsync(Guid sectionId, CancellationToken cancellationToken = default);

    Task<Guid?> GetClassTeacherUserIdAsync(Guid sectionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSectionIdsForTeacherAsync(
        Guid teacherEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the class-teacher assignment for a section (clears teacherid on other rows if needed).
    /// Pass <paramref name="teacherId"/> null to clear.
    /// </summary>
    Task UpsertClassTeacherAsync(
        Guid sectionId,
        Guid? classGroupId,
        Guid? teacherId,
        CancellationToken cancellationToken = default);
}
