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
    /// <summary>
    ///     String that is expected to yield no search results.
    /// </summary>
    internal const string NoAccessSentinel = "__upload_scope_no_access__";

    public static bool IsUnrestricted(ClaimsPrincipal user)
    {
        return user.HasClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.Unrestricted);
    }

    public static bool IsValidator(ClaimsPrincipal user)
    {
        return user.HasClaim(BespokeAuthorizationPermissions.ClaimType,
            BespokeAuthorizationPermissions.UploadValidator);
    }
}