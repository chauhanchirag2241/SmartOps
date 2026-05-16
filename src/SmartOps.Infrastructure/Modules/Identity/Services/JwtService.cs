using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartOps.Application.Configuration;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.MultiTenancy;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _options;
    private readonly TenantContext _tenantContext;

    public JwtService(IOptions<JwtOptions> options, TenantContext tenantContext)
    {
        _options = options.Value;
        _tenantContext = tenantContext;
    }

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        };

        string? tenantId = _tenantContext.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim(JwtCustomClaimNames.TenantId, tenantId));
        }

        string? schoolId = _tenantContext.SchoolId;
        if (!string.IsNullOrWhiteSpace(schoolId))
        {
            claims.Add(new Claim(JwtCustomClaimNames.SchoolId, schoolId));
        }

        foreach (string role in roles.Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.SecretKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        DateTime utcNow = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.AddMinutes(_options.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        JwtSecurityTokenHandler handler = new();
        return handler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        byte[] bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        JwtSecurityTokenHandler handler = new();
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_options.SecretKey));

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(token, parameters, out SecurityToken _);
            return principal;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }
}
