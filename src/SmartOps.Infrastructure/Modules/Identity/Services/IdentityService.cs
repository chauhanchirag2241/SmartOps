using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Authorization.DTOs;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Shared.Common;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IJwtService _jwtService;
    private readonly IUserCredentialValidator _credentialValidator;
    private readonly IUserScopeService _userScopeService;
    private readonly TenantContext _tenantContext;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IMenuRepository menuRepository,
        IJwtService jwtService,
        IUserCredentialValidator credentialValidator,
        IUserScopeService userScopeService,
        TenantContext tenantContext,
        IOptions<JwtOptions> jwtOptions,
        ILogger<IdentityService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _menuRepository = menuRepository;
        _jwtService = jwtService;
        _credentialValidator = credentialValidator;
        _userScopeService = userScopeService;
        _tenantContext = tenantContext;
        _jwtOptions = jwtOptions;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = request.Email.Trim().ToLowerInvariant();

        ApplicationUser? user = await _userRepository
            .GetByEmailAsync(normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result<LoginResponseDto>.Failure("Invalid email or password.");
        }

        bool passwordValid = await _credentialValidator
            .VerifyPasswordAsync(user, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!passwordValid)
        {
            return Result<LoginResponseDto>.Failure("Invalid email or password.");
        }

        IList<string> roles = await _userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        (string accessToken, string refreshTokenValue) = await CreateTokenPairAsync(user, roles, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        LoginResponseDto response = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = _jwtOptions.Value.AccessTokenExpiryMinutes * 60
        };

        return Result<LoginResponseDto>.Success(response);
    }

    public async Task<Result<LoginResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        RefreshToken? existing = await _refreshTokenRepository
            .GetByTokenAsync(refreshToken, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null || !existing.IsActive || existing.IsRevoked)
        {
            return Result<LoginResponseDto>.Failure("Invalid refresh token.");
        }

        DateTime utcNow = DateTime.UtcNow;
        if (existing.ExpiresAt < utcNow)
        {
            return Result<LoginResponseDto>.Failure("Refresh token has expired.");
        }

        ApplicationUser? user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Result<LoginResponseDto>.Failure("User is not available.");
        }

        IList<string> roles = await _userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _refreshTokenRepository.RevokeAsync(refreshToken, cancellationToken).ConfigureAwait(false);

        (string accessToken, string refreshTokenValue) = await CreateTokenPairAsync(user, roles, cancellationToken).ConfigureAwait(false);

        LoginResponseDto response = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = _jwtOptions.Value.AccessTokenExpiryMinutes * 60
        };

        return Result<LoginResponseDto>.Success(response);
    }

    public async Task<Result<UserScopeDto>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Guid? schoolId = Guid.TryParse(_tenantContext.SchoolId, out Guid sid) ? sid : null;
        UserScopeDto scope = await _userScopeService
            .GetScopeAsync(userId, schoolId, cancellationToken)
            .ConfigureAwait(false);
        return Result<UserScopeDto>.Success(scope);
    }

    private async Task<(string AccessToken, string RefreshToken)> CreateTokenPairAsync(
        ApplicationUser user,
        IList<string> roles,
        CancellationToken cancellationToken)
    {
        Guid? schoolId = Guid.TryParse(_tenantContext.SchoolId, out Guid sid) ? sid : null;
        UserScopeDto scope = await _userScopeService
            .GetScopeAsync(user.Id, schoolId, cancellationToken)
            .ConfigureAwait(false);

        string accessToken = _jwtService.GenerateAccessToken(
            user,
            roles,
            scope.ScopeVersion,
            scope.ScopeType);

        string refreshTokenValue = _jwtService.GenerateRefreshToken();
        JwtOptions jwt = _jwtOptions.Value;
        DateTime utcNow = DateTime.UtcNow;

        RefreshToken refreshEntity = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = utcNow.AddDays(jwt.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedBy = user.Id,
            CreatedOn = utcNow,
            UpdatedBy = user.Id,
            UpdatedOn = utcNow,
            IsActive = true,
            VersionNo = 1
        };

        await _refreshTokenRepository.CreateAsync(refreshEntity, cancellationToken).ConfigureAwait(false);
        return (accessToken, refreshTokenValue);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _refreshTokenRepository.RevokeAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userRepository
            .GetByIdAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            return Result<UserDto>.Failure("User was not found.");
        }

        IList<string> roles = await _userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        (Guid RoleId, string RoleName, string RoleCode)? primaryRole =
            await _userRepository.GetPrimaryRoleAsync(user.Id, cancellationToken).ConfigureAwait(false);

        UserDto dto = user.ToUserDto();
        dto.Roles = roles.ToList();
        if (primaryRole is not null)
        {
            dto.RoleId = primaryRole.Value.RoleId;
            dto.RoleCode = primaryRole.Value.RoleCode;
        }

        return Result<UserDto>.Success(dto);
    }

    public async Task<Result<UserPermissionResponseDto>> GetUserPermissionsAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return Result<UserPermissionResponseDto>.Failure("User was not found.");
        }

        (Guid RoleId, string RoleName, string RoleCode)? primaryRole =
            await _userRepository.GetPrimaryRoleAsync(userId, cancellationToken).ConfigureAwait(false);

        if (primaryRole is null)
        {
            return Result<UserPermissionResponseDto>.Failure("User has no assigned role.");
        }

        IReadOnlyList<MenuPermissionDto> permissions = await _menuRepository
            .GetUserMenuPermissionsForApplicationAsync(userId, application, cancellationToken)
            .ConfigureAwait(false);

        UserPermissionResponseDto response = new()
        {
            UserId = userId,
            RoleId = primaryRole.Value.RoleId,
            RoleName = primaryRole.Value.RoleName,
            RoleCode = primaryRole.Value.RoleCode,
            Permissions = permissions
        };

        return Result<UserPermissionResponseDto>.Success(response);
    }
}
