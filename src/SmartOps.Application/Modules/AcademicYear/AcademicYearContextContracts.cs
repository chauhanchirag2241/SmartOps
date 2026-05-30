namespace SmartOps.Application.Modules.AcademicYear;

public interface IAcademicYearContext
{
    bool IsResolved { get; }

    Guid? CurrentAcademicYearId { get; }

    Guid? SelectedAcademicYearId { get; }

    Guid? EffectiveAcademicYearId { get; }

    bool CanSwitchAcademicYear { get; }

    /// <summary>
    /// True when the effective year is not the school's current year — data changes are blocked.
    /// </summary>
    bool IsReadOnlyAcademicYear { get; }

    Task EnsureResolvedAsync(CancellationToken cancellationToken = default);
}
