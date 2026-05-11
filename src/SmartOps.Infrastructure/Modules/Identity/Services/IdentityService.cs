using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Shared.Common;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;

    private readonly IRefreshTokenRepository _refreshTokenRepository;

    private readonly IJwtService _jwtService;

    private readonly IUserCredentialValidator _credentialValidator;

    private readonly IOptions<JwtOptions> _jwtOptions;

    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtService jwtService,
        IUserCredentialValidator credentialValidator,
        IOptions<JwtOptions> jwtOptions,
        ILogger<IdentityService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtService = jwtService;
        _credentialValidator = credentialValidator;
        _jwtOptions = jwtOptions;
        _logger = logger;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ApplicationUser? user = await _userRepository
            .GetByEmailAsync(request.Email, cancellationToken)
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
        IList<string> permissions = await _userRepository.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        string accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
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

        LoginResponseDto response = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = jwt.AccessTokenExpiryMinutes * 60
        };

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

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
        IList<string> permissions = await _userRepository.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        string accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
        string refreshTokenValue = _jwtService.GenerateRefreshToken();

        JwtOptions jwt = _jwtOptions.Value;

        await _refreshTokenRepository.RevokeAsync(refreshToken, cancellationToken).ConfigureAwait(false);

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

        LoginResponseDto response = new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = jwt.AccessTokenExpiryMinutes * 60
        };

        return Result<LoginResponseDto>.Success(response);
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
        IList<string> permissions = await _userRepository.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        UserDto dto = user.ToUserDto();
        dto.Roles = roles.ToList();
        dto.Permissions = permissions.ToList();

        return Result<UserDto>.Success(dto);
    }
}
