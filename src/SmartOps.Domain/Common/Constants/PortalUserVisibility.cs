namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Platform / bootstrap accounts that must never appear in school portal user lists.
/// </summary>
public static class PortalUserVisibility
{
    public const string SmartOpsAdminEmail = "admin@smartops.com";
    public const string PlatformAdminUsername = "platform.admin";

    /// <summary>Seeded global platform admin user id (G012).</summary>
    public static readonly Guid PlatformAdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static bool IsHiddenFromPortal(
        string? email,
        string? username = null,
        Guid? userId = null,
        IEnumerable<string>? roleNames = null,
        Guid? userTypeId = null)
    {
        if (userId is Guid id && id == PlatformAdminUserId)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(email)
            && string.Equals(email.Trim(), SmartOpsAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(username)
            && string.Equals(username.Trim(), PlatformAdminUsername, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (userTypeId == UserTypeCodes.Ids.Admin)
        {
            return true;
        }

        if (roleNames is not null)
        {
            foreach (string role in roleNames)
            {
                if (RoleNames.IsHiddenFromPortal(role))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
