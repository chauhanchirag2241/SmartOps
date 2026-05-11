using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Shared.Common;

namespace SmartOps.Api.Modules.Identity;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;

    private readonly IValidator<LoginRequestDto> _loginValidator;

    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;

    public AuthController(
        IIdentityService identityService,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator)
    {
        _identityService = identityService;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        ValidationResult validation = await _loginValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        }

        Result<LoginResponseDto> result = await _identityService.LoginAsync(request, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(result.Error);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        ValidationResult validation = await _refreshTokenValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        }

        Result<LoginResponseDto> result =
            await _identityService.RefreshTokenAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Error);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        ValidationResult validation = await _refreshTokenValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Errors.Select(e => e.ErrorMessage));
        }

        await _identityService.LogoutAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out Guid userId))
        {
            return Unauthorized();
        }

        Result<UserDto> result = await _identityService.GetCurrentUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized();
    }
}
