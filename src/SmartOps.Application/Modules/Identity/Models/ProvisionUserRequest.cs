namespace SmartOps.Application.Modules.Identity.Models;

/// <summary>Input for the shared school-user provisioning pipeline.</summary>
public sealed class ProvisionUserRequest
{
    public required Guid SchoolId { get; init; }

    public required string RoleName { get; init; }

    /// <summary>When set, overrides automatic role-to-user-type mapping.</summary>
    public string? UserTypeCode { get; init; }

    public bool PortalAccess { get; init; }

    public string? Email { get; init; }

    public string? Username { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Fallback identifier when email is absent (e.g. admission number).</summary>
    public string? LoginIdentifier { get; init; }
}
