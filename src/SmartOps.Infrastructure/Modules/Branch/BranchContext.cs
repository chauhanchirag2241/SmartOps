using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Branch.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.MultiTenancy;

namespace SmartOps.Infrastructure.Modules.Branch;

public sealed class BranchContext : IBranchContext
{
    public const string ActiveBranchHeader = "X-Branch-Id";
    public const string SelectedBranchesHeader = "X-Branch-Ids";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IBranchRepository _branchRepository;
    private readonly IUserRepository _userRepository;
    private readonly TenantContext _tenantContext;
    private bool _resolved;

    public BranchContext(
        IHttpContextAccessor httpContextAccessor,
        IBranchRepository branchRepository,
        IUserRepository userRepository,
        TenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _branchRepository = branchRepository;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public bool IsResolved => _resolved;

    public IReadOnlyList<Guid> AllowedBranchIds { get; private set; } = [];

    public Guid? ActiveBranchId { get; private set; }

    public IReadOnlyList<Guid> SelectedBranchIds { get; private set; } = [];

    public bool CanViewAllBranches { get; private set; }

    public async Task EnsureResolvedAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
        {
            return;
        }

        if (!Guid.TryParse(_tenantContext.SchoolId, out Guid schoolId))
        {
            _resolved = true;
            return;
        }

        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            _resolved = true;
            return;
        }

        IList<string> roleCodes = await _userRepository
            .GetRoleCodesAsync(userId.Value, cancellationToken)
            .ConfigureAwait(false);

        CanViewAllBranches = roleCodes.Any(c => RoleCodes.GlobalScopeRoles.Contains(c));

        if (CanViewAllBranches)
        {
            IReadOnlyList<BranchDropdownItemDto> allBranches = await _branchRepository
                .GetBranchesBySchoolAsync(schoolId, cancellationToken)
                .ConfigureAwait(false);
            AllowedBranchIds = allBranches.Select(b => b.Id).ToList();
        }
        else
        {
            AllowedBranchIds = await _branchRepository
                .GetUserBranchIdsAsync(userId.Value, schoolId, cancellationToken)
                .ConfigureAwait(false);
        }

        ActiveBranchId = ResolveActiveBranchId();
        SelectedBranchIds = ResolveSelectedBranchIds();

        _resolved = true;
    }

    public bool HasBranchAccess(Guid branchId) =>
        CanViewAllBranches || AllowedBranchIds.Contains(branchId);

    private Guid? ResolveActiveBranchId()
    {
        if (AllowedBranchIds.Count == 0)
        {
            return null;
        }

        if (TryReadHeaderBranchId(out Guid headerBranchId) && HasBranchAccess(headerBranchId))
        {
            return headerBranchId;
        }

        return AllowedBranchIds[0];
    }

    private IReadOnlyList<Guid> ResolveSelectedBranchIds()
    {
        if (AllowedBranchIds.Count == 0)
        {
            return [];
        }

        string? raw = _httpContextAccessor.HttpContext?.Request.Headers[SelectedBranchesHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            List<Guid> parsed = raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out Guid id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty && HasBranchAccess(id))
                .Distinct()
                .ToList();

            if (parsed.Count > 0)
            {
                return parsed;
            }
        }

        return ActiveBranchId.HasValue ? [ActiveBranchId.Value] : AllowedBranchIds.ToList();
    }

    private bool TryReadHeaderBranchId(out Guid branchId)
    {
        branchId = Guid.Empty;
        string? raw = _httpContextAccessor.HttpContext?.Request.Headers[ActiveBranchHeader].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out branchId);
    }

    private Guid? GetCurrentUserId()
    {
        string? sub = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
