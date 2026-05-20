using System.Security.Claims;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(
        ApplicationUser user,
        IList<string> roles,
        int scopeVersion = 1,
        DataScopeType scopeType = DataScopeType.Global);

    string GenerateRefreshToken();

    ClaimsPrincipal? ValidateToken(string token);
}
