using Microsoft.AspNetCore.Http;
using SmartOps.Application.Modules.AcademicYear;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Application.Modules.Identity.Interfaces;
using System.Security.Claims;

namespace SmartOps.Infrastructure.Modules.AcademicYear;

public sealed class AcademicYearContext : IAcademicYearContext
{
    public const string HeaderName = "X-Academic-Year-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IUserRepository _userRepository;
    private bool _resolved;

    public AcademicYearContext(
        IHttpContextAccessor httpContextAccessor,
        IAcademicYearRepository academicYearRepository,
        IUserRepository userRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _academicYearRepository = academicYearRepository;
        _userRepository = userRepository;
    }

    public bool IsResolved => _resolved;

    public Guid? CurrentAcademicYearId { get; private set; }

    public Guid? SelectedAcademicYearId { get; private set; }

    public Guid? EffectiveAcademicYearId => SelectedAcademicYearId ?? CurrentAcademicYearId;

    public bool CanSwitchAcademicYear { get; private set; }

    public bool IsReadOnlyAcademicYear { get; private set; }

    public async Task EnsureResolvedAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return;
        }

        CurrentAcademicYearId = await _academicYearRepository
            .GetCurrentAcademicYearIdAsync(cancellationToken)
            .ConfigureAwait(false);

        CanSwitchAcademicYear = false;
        SelectedAcademicYearId = null;

        Guid? userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            IList<string> roleCodes = await _userRepository
                .GetRoleCodesAsync(userId.Value, cancellationToken)
                .ConfigureAwait(false);

            CanSwitchAcademicYear = roleCodes.Any(c =>
                RoleCodes.GlobalScopeRoles.Contains(c));
        }

        if (CanSwitchAcademicYear && TryReadHeaderYearId(out Guid headerYearId))
        {
            bool exists = await _academicYearRepository
                .AcademicYearExistsAsync(headerYearId, requireNotDeleted: true, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                SelectedAcademicYearId = headerYearId;
            }
        }

        Guid? effective = EffectiveAcademicYearId;
        if (!CurrentAcademicYearId.HasValue || !effective.HasValue || effective.Value == CurrentAcademicYearId.Value)
        {
            IsReadOnlyAcademicYear = false;
        }
        else
        {
            IsReadOnlyAcademicYear = await _academicYearRepository
                .IsAcademicYearBeforeAsync(effective.Value, CurrentAcademicYearId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        _resolved = true;
    }

    private bool TryReadHeaderYearId(out Guid yearId)
    {
        yearId = Guid.Empty;
        string? raw = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out yearId);
    }

    private Guid? GetCurrentUserId()
    {
        string? sub = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
