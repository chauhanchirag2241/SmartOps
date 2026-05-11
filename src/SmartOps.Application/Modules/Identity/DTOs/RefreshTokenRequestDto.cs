namespace SmartOps.Application.Modules.Identity.DTOs;

public sealed class RefreshTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}
