namespace SmartOps.Application.Modules.Identity.Models;

/// <summary>Input for the shared school-user provisioning pipeline.</summary>
public sealed class ProvisionUserRequest
{
    public required Guid SchoolId { get; init; }

    /// <summary>Optional portal role; when omitted, no role is assigned (Admin is the only seeded role).</summary>
    public string? RoleName { get; init; }

    public required string UserTypeCode { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? Mobile { get; init; }

    /// <summary>Optional explicit username; default is firstname.lastname.</summary>
    public string? Username { get; init; }
}
