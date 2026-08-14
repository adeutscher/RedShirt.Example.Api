using Microsoft.AspNetCore.Authentication;
using RedShirt.Example.Api.Authorization.Constants;
using System.Security.Claims;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Translates <see cref="BespokeAuthorizationPermissions.ClaimType" /> permission claims from realm roles
///     in bearer token using <see cref="BespokeRolePermissionMap" /> to enrich a <see cref="ClaimsPrincipal" />.
/// </summary>
internal sealed class BespokeRolePermissionClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // ReSharper disable once DuplicatedSequentialIfBodies
        if (principal.Identity is not ClaimsIdentity {IsAuthenticated: true} identity)
        {
            // Not an identity.
            return Task.FromResult(principal);
        }

        if (identity.HasClaim(claim => claim.Type == BespokeAuthorizationPermissions.ClaimType))
        {
            // Identity already has permissions (already processed through another TransformAsync call?)
            return Task.FromResult(principal);
        }

        var roles = identity.FindAll(identity.RoleClaimType)
            .Concat(identity.FindAll("role"))
            .Select(claim => claim.Value);
        foreach (var permission in BespokeRolePermissionMap.GetPermissions(roles))
        {
            identity.AddClaim(new Claim(BespokeAuthorizationPermissions.ClaimType, permission));
        }

        return Task.FromResult(principal);
    }
}