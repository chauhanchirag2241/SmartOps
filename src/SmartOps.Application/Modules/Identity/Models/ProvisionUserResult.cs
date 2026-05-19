namespace SmartOps.Application.Modules.Identity.Models;

public sealed class ProvisionUserResult
{
    public required Guid UserId { get; init; }

    public bool IsNewUser { get; init; }

    /// <summary>Plain-text password for newly created users (null when reusing an existing account).</summary>
    public string? GeneratedPassword { get; init; }
}
