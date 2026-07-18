namespace SmartOps.Domain.Modules.AcademicPeriod;

public interface IAcademicPeriodRepository
{
    Task<IReadOnlyList<AcademicPeriodClassSummary>> GetClassesAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassAcademicPeriodEntity>> GetByClassAsync(
        Guid classId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid classId,
        Guid academicYearId,
        IReadOnlyList<ClassAcademicPeriodEntity> periods,
        CancellationToken cancellationToken = default);

    Task<bool> HasPaidInstallmentsAsync(
        Guid classId,
        CancellationToken cancellationToken = default);
}
