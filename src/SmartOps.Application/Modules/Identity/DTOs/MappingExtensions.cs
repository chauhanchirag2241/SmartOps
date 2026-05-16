using SmartOps.Application.Modules.School.DTOs;
using SmartOps.Domain.Modules.Identity.Entities;
using SchoolEntity = SmartOps.Domain.Modules.School.Entities.SchoolEntity;

namespace SmartOps.Application.Modules.Identity.DTOs;

public static class IdentityMappingExtensions
{
    public static UserDto ToUserDto(this ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        IsActive = user.IsActive,
        CreatedOn = user.CreatedOn
    };

    public static SchoolDto ToDto(this SchoolEntity school) => new()
    {
        Id = school.Id,
        Name = school.Name,
        Subdomain = school.Subdomain,
        IsActive = school.IsActive
    };
}
