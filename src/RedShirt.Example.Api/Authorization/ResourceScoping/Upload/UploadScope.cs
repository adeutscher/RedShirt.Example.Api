using RedShirt.Example.Api.Authorization.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization.ResourceScoping.Upload;

/// <summary>
///     Shared rules for upload-scoped resources:
///     <see cref="BespokeAuthorizationPermissions.Unrestricted" /> bypasses owner scope;
///     otherwise the caller is limited to their JWT <c>sub</c> / name identifier.
/// </summary>
internal static class UploadScope
{
    internal const string NoAccessSentinel = "__upload_scope_no_access__";

    public static bool IsUnrestricted(ClaimsPrincipal user)
    {
        return user.HasClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Unrestricted);
    }

    public static bool IsValidator(ClaimsPrincipal user)
    {
        return user.HasClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadValidator);
    }

    public static bool TryGetUserId(ClaimsPrincipal user, out string userId)
    {
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue("sub")
                 ?? string.Empty;
        return !string.IsNullOrWhiteSpace(userId);
    }
}
