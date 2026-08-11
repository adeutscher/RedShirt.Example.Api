using System.Security.Claims;
using RedShirt.Example.Api.Constants;

namespace RedShirt.Example.Api.Authorization;

/// <summary>
///     Shared rules for customer-scoped resources: <see cref="AuthorizationPermissions.Write" />
///     is unrestricted; otherwise the caller is limited to
///     <see cref="AuthorizationClaims.CustomerId" />.
/// </summary>
internal static class CustomerScope
{
    public static bool IsUnrestricted(ClaimsPrincipal user)
    {
        return user.HasClaim(AuthorizationPermissions.ClaimType, AuthorizationPermissions.Write);
    }

    public static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var value = user.FindFirst(AuthorizationClaims.CustomerId)?.Value;
        return Guid.TryParse(value, out customerId);
    }
}
