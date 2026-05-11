namespace SmartOps.Application.Modules.Identity.DTOs;

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }
}
