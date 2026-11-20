using Microsoft.AspNetCore.Authentication;
using RedShirt.Example.Api.Authorization.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Translates realm roles in the bearer token into <see cref="BespokeAuthorizationPermissions.ClaimType" />
///     claims using <see cref="BespokeRolePermissionMap" />.
/// </summary>
/// <remarks>
///     Permissions are owned by this API, never by the token. Any permission claims that arrive in the token are removed,
///     and the mapped permissions are added on a separate identity with
///     <see cref="PermissionsAuthenticationType" />. That identity also marks the principal as already transformed,
///     because ASP.NET Core may call <see cref="TransformAsync" /> more than once per request.
/// </remarks>
internal sealed class BespokeRolePermissionClaimsTransformation(
    ILogger<BespokeRolePermissionClaimsTransformation> logger) : IClaimsTransformation
{
    /// <summary>
    ///     Authentication type of the identity that holds the permissions mapped by this API.
    /// </summary>
    internal const string PermissionsAuthenticationType = "RedShirt.Example.Api.Permissions";

    private int _warnedAboutTokenPermissions;

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity {IsAuthenticated: true} identity)
        {
            // Not an identity.
            return Task.FromResult(principal);
        }

        if (principal.Identities.Any(candidate => candidate.AuthenticationType == PermissionsAuthenticationType))
        {
            // Already processed through another TransformAsync call.
            return Task.FromResult(principal);
        }

        RemoveTokenPermissions(principal);

        var roles = identity.FindAll(identity.RoleClaimType)
            .Concat(identity.FindAll("role"))
            .Select(claim => claim.Value);
        var permissions = new ClaimsIdentity(PermissionsAuthenticationType);
        foreach (var permission in BespokeRolePermissionMap.GetPermissions(roles))
        {
            permissions.AddClaim(new Claim(BespokeAuthorizationPermissions.ClaimType, permission));
        }

        principal.AddIdentity(permissions);
        return Task.FromResult(principal);
    }

    private void RemoveTokenPermissions(ClaimsPrincipal principal)
    {
        var removed = 0;
        foreach (var identity in principal.Identities)
        {
            foreach (var claim in identity.FindAll(BespokeAuthorizationPermissions.ClaimType).ToList())
            {
                if (identity.TryRemoveClaim(claim))
                {
                    removed++;
                }
            }
        }

        if (removed > 0 && Interlocked.Exchange(ref _warnedAboutTokenPermissions, 1) == 0)
        {
            logger.LogWarning(
                "Ignored {Count} '{ClaimType}' claim(s) supplied by the identity provider. Permissions are mapped from "
                + "roles by this API. Check the identity provider for a mapper that emits this claim. This warning is "
                + "logged once per process.",
                removed, BespokeAuthorizationPermissions.ClaimType);
        }
    }
}
