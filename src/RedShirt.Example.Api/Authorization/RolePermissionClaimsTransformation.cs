using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Adds <see cref="AuthorizationPermissions.ClaimType" /> claims from realm roles
///     using <see cref="RolePermissionMap" />.
/// </summary>
internal sealed class RolePermissionClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity {IsAuthenticated: true} identity)
        {
            return Task.FromResult(principal);
        }

        if (identity.HasClaim(claim => claim.Type == AuthorizationPermissions.ClaimType))
        {
            return Task.FromResult(principal);
        }

        var roles = identity.FindAll(identity.RoleClaimType)
            .Concat(identity.FindAll("role"))
            .Select(claim => claim.Value);
        foreach (var permission in RolePermissionMap.GetPermissions(roles))
        {
            identity.AddClaim(new Claim(AuthorizationPermissions.ClaimType, permission));
        }

        return Task.FromResult(principal);
    }
}
