namespace SmartOps.Application.Modules.AcademicYear;

public interface IAcademicYearContext
{
    bool IsResolved { get; }

    Guid? CurrentAcademicYearId { get; }

    Guid? SelectedAcademicYearId { get; }

    Guid? EffectiveAcademicYearId { get; }

    bool CanSwitchAcademicYear { get; }

    /// <summary>
    /// True when the effective year is a past academic year (before the school's current year).
    /// Current and future years allow add/edit for setup and operations.
    /// </summary>
    bool IsReadOnlyAcademicYear { get; }

    Task EnsureResolvedAsync(CancellationToken cancellationToken = default);
}
